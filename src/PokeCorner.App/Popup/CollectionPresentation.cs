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
    private readonly IReadOnlyDictionary<int, string> _routes;
    private static readonly Lazy<IReadOnlyDictionary<int, string>> Facts = new(() =>
    {
        using var stream = typeof(CollectionPresentation).Assembly.GetManifestResourceStream(
            "PokeCorner.Assets.UiSystem.curated-russian-facts.json")
            ?? typeof(CollectionPresentation).Assembly.GetManifestResourceStream(
            "PokeCorner.App.Assets.UiSystem.curated-russian-facts.json");
        if (stream is null) return new Dictionary<int, string>();
        using var json = JsonDocument.Parse(stream);
        return json.RootElement.GetProperty("facts").EnumerateArray()
            .ToDictionary(row => row.GetProperty("speciesId").GetInt32(), row => row.GetProperty("textRu").GetString()!);
    });
    public static string? Fact(int speciesId) => Facts.Value.GetValueOrDefault(speciesId);

    public CollectionPresentation(CatalogPackage catalog)
    {
        _catalog = catalog; _routes = catalog.GetRouteHints(); _speciesById = catalog.Species.ToDictionary(s => s.Id);
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

public sealed record IndividualItemPreview(string ItemId, string Name, int Stock, string Effect, string? Reason);
