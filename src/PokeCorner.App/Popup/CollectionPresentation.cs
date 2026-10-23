using System.IO;
using System.Text.Json;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Popup;

/// Offline presentation data; never uses review fixtures or performs per-card IO/network.
public sealed class CollectionPresentation
{
    private readonly CatalogPackage _catalog;
    private readonly Dictionary<int, NurseryRecipe[]> _recipesByOutcome;
    private readonly Dictionary<int, CatalogSpecies> _speciesById;
    private readonly Dictionary<int, int[]> _predecessors;
    public IReadOnlyDictionary<int, int[]> Targets { get; }
    private readonly IReadOnlyDictionary<int, string> _routes, _routeIds;
    private static readonly Lazy<IReadOnlyDictionary<int, string>> Facts = new(() =>
        LoadFacts(Path.Combine(AppContext.BaseDirectory, "Data", "catalog-package", "russian-facts.json")));
    public static string? Fact(int speciesId) => Facts.Value.GetValueOrDefault(speciesId);

    private static IReadOnlyDictionary<int, string> LoadFacts(string path)
    {
        var facts = new Dictionary<int, string>();
        try
        {
            using var stream = File.OpenRead(path);
            using var json = JsonDocument.Parse(stream);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("facts", out var rows) || rows.ValueKind != JsonValueKind.Array)
                return facts;
            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object
                    || !row.TryGetProperty("speciesId", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt32(out var speciesId)
                    || speciesId is < 1 or > 1025
                    || !row.TryGetProperty("textRu", out var text) || text.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(text.GetString())
                    || !text.GetString()!.Any(c => c is >= '\u0400' and <= '\u04ff')
                    || !facts.TryAdd(speciesId, text.GetString()!))
                    return new Dictionary<int, string>();
            }
            return facts;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Dictionary<int, string>();
        }
    }

    public CollectionPresentation(CatalogPackage catalog)
    {
        _catalog = catalog; _routes = catalog.GetRouteHints(); _routeIds = catalog.GetRouteIds(); _speciesById = catalog.Species.ToDictionary(s => s.Id);
        var nursery = new NurseryCatalog(catalog);
        _recipesByOutcome = nursery.MixedRecipes.Concat(nursery.SameSpeciesRecipes).Where(r => r.IsAvailable)
            .SelectMany(r => r.AvailableOutcomes.Select(o => (o.SpeciesId, Recipe: r)))
            .GroupBy(x => x.SpeciesId).ToDictionary(g => g.Key, g => g.Select(x => x.Recipe).Take(3).ToArray());
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "catalog-rules.json")));
        var edges = json.RootElement.GetProperty("evolutionEdges").EnumerateArray()
            .Select(e => (From: e.GetProperty("from").GetInt32(), To: e.GetProperty("to").GetInt32())).ToArray();
        Targets = edges.GroupBy(e => e.From).ToDictionary(g => g.Key, g => g.Select(e => e.To).ToArray());
        _predecessors = edges.GroupBy(e => e.To).ToDictionary(g => g.Key, g => g.Select(e => e.From).ToArray());
    }

    /// The same facts as WhereFind, as actions: a route to open and parent pairs to preset.
    public SpeciesPaths Paths(int speciesId, bool shiny, IReadOnlySet<int> known)
    {
        var species = _speciesById[speciesId];
        string Name(int id) => known.Contains(id) ? _speciesById[id].DisplayName : $"#{id:D4}";
        if (species.FamilyRootId != speciesId)
            return new(null, null, [], "Эволюция из " + string.Join(" / ", (_predecessors.GetValueOrDefault(speciesId) ?? [species.FamilyRootId]).Select(Name)) + ".");
        if (!_catalog.CanIssueDirectly(speciesId, shiny)) return new(null, null, [], "Эта форма пока недоступна для получения.");
        var routeId = _routeIds.GetValueOrDefault(speciesId);
        var pairs = (_recipesByOutcome.GetValueOrDefault(speciesId) ?? [])
            .Select(r => new SpeciesPair(r.FirstParentId, r.SecondParentId, $"{Name(r.FirstParentId)} + {Name(r.SecondParentId)}")).ToArray();
        return new(routeId, routeId is null ? null : _routes.GetValueOrDefault(speciesId), pairs, null);
    }

    public string WhereFind(int speciesId, bool shiny, IReadOnlySet<int> known)
    {
        var species = _speciesById[speciesId];
        string Name(int id) => known.Contains(id) ? _speciesById[id].DisplayName : $"#{id:D4}";
        if (species.FamilyRootId != speciesId)
            return "Эволюция из " + string.Join(" / ", (_predecessors.GetValueOrDefault(speciesId) ?? [species.FamilyRootId]).Select(Name))
                + ". Вырастите предшественника и откройте его карточку.";
        if (!_catalog.CanIssueDirectly(speciesId, shiny)) return "Эта форма пока недоступна для получения. Открытая история сохраняется.";
        var text = "Встречи: " + _routes.GetValueOrDefault(speciesId, "маршрут пока неизвестен") + ".";
        var recipes = _recipesByOutcome.GetValueOrDefault(speciesId) ?? [];
        if (recipes.Length > 0) text += " Питомник: " + string.Join("; ", recipes.Select(r => $"{Name(r.FirstParentId)} + {Name(r.SecondParentId)}")) + ".";
        return text;
    }
}

public sealed record SpeciesPair(int FirstParentId, int SecondParentId, string Label);
public sealed record SpeciesPaths(string? RouteId, string? RouteName, IReadOnlyList<SpeciesPair> Pairs, string? Note);

public sealed record IndividualItemPreview(string ItemId, string Name, int Stock, string Effect, string? Reason);
