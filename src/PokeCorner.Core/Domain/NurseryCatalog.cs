using System.Text.Json;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Domain;

public sealed class NurseryCatalog
{
    private readonly Dictionary<(int, int), NurseryRecipe> byPair = [];
    private readonly Dictionary<int, int> familyRoots;
    private readonly Dictionary<int, IReadOnlyList<int>> partners;

    public IReadOnlyList<NurseryRecipe> MixedRecipes { get; }
    public IReadOnlyList<NurseryRecipe> SameSpeciesRecipes { get; }

    public NurseryCatalog(CatalogPackage catalog)
    {
        familyRoots = catalog.Species.ToDictionary(s => s.Id, s => s.FamilyRootId);
        using var rules = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "catalog-rules.json")));
        using var balance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "balance.json")));
        var source = rules.RootElement.GetProperty("nurseryRecipeSource").GetString()
            ?? throw new InvalidDataException("Missing nursery recipe source.");
        var dataRoot = Path.GetFullPath(catalog.DataRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var recipePath = Path.GetFullPath(Path.Combine(dataRoot, "final", source));
        if (!recipePath.StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Nursery recipe source escapes the package.");
        using var recipes = JsonDocument.Parse(File.ReadAllBytes(recipePath));
        if (recipes.RootElement.GetProperty("dataRevision").GetString() != catalog.CatalogRevision)
            throw new InvalidDataException("Nursery recipe revision mismatch.");

        var hoursByRarity = balance.RootElement.GetProperty("nurseryHours").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        var rarityOrder = balance.RootElement.GetProperty("rarityOrder").EnumerateArray().Select(x => x.GetString()!).ToArray();
        var weights = balance.RootElement.GetProperty("nurseryWeights").EnumerateArray()
            .Select(x => (int)(x.GetDouble() * 2)).ToArray();
        int Weight(string rarity)
        {
            var index = Array.IndexOf(rarityOrder, rarity);
            if (index < 0) throw new InvalidDataException($"Unknown nursery rarity: {rarity}");
            return weights[index];
        }
        int? Hours(IReadOnlyList<NurseryOutcome> available) => available.Count == 0 ? null :
            hoursByRarity[available.Max(o => Array.IndexOf(rarityOrder, o.Rarity))];
        static Dictionary<string, int> Berries(JsonElement element)
        {
            var berries = element.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetInt32());
            if (berries.Any(x => x.Key != x.Key.ToLowerInvariant() || x.Value <= 0))
                throw new InvalidDataException("Invalid nursery berries.");
            return berries;
        }

        var mixed = new List<NurseryRecipe>();
        foreach (var item in recipes.RootElement.GetProperty("recipes").EnumerateArray())
        {
            var ids = item.GetProperty("parentIds").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            var outcomes = item.GetProperty("outcomes").EnumerateArray().Select(x =>
            {
                var id = x.GetProperty("speciesId").GetInt32();
                var rarity = x.GetProperty("rarity").GetString()!;
                ValidateRoot(id);
                return new NurseryOutcome(id, rarity, Weight(rarity), catalog.CanObtain(id, true));
            }).ToArray();
            var available = outcomes.Where(o => catalog.CanObtain(o.SpeciesId, false)).ToArray();
            var enabled = ids.All(id => catalog.CanObtain(id, false));
            var recipe = new NurseryRecipe(ids[0], ids[1], outcomes, enabled ? available : [],
                Berries(item.GetProperty("baseBerries")), enabled && available.Length > 1
                    ? Berries(item.GetProperty("selectedSurcharge")) : new Dictionary<string, int>(),
                item.GetProperty("needsThemeReview").GetBoolean(), enabled ? Hours(available) : null);
            mixed.Add(recipe);
            if (!byPair.TryAdd(Key(ids[0], ids[1]), recipe)) throw new InvalidDataException("Duplicate nursery pair.");
        }
        MixedRecipes = mixed.AsReadOnly();

        var same = new List<NurseryRecipe>();
        foreach (var item in recipes.RootElement.GetProperty("sameSpeciesRecipes").EnumerateArray())
        {
            var parent = item.GetProperty("speciesId").GetInt32();
            var child = item.GetProperty("outcomeSpeciesId").GetInt32();
            ValidateRoot(child);
            if (!familyRoots.TryGetValue(parent, out var root) || root != child)
                throw new InvalidDataException($"Wrong same-species family root: {parent}");
            var rarity = catalog.Species.Single(s => s.Id == child).Rarity;
            var outcome = new NurseryOutcome(child, rarity, Weight(rarity), catalog.CanObtain(child, true));
            var enabled = catalog.CanObtain(parent, false) && catalog.CanObtain(child, false);
            var recipe = new NurseryRecipe(parent, parent, [outcome], enabled ? [outcome] : [],
                Berries(item.GetProperty("baseBerries")), Berries(item.GetProperty("selectedSurcharge")),
                false, enabled ? Hours([outcome]) : null);
            same.Add(recipe);
            if (!byPair.TryAdd((parent, parent), recipe)) throw new InvalidDataException("Duplicate nursery pair.");
        }
        SameSpeciesRecipes = same.AsReadOnly();
        partners = byPair.Values.Where(r => r.IsAvailable)
            .SelectMany(r => r.FirstParentId == r.SecondParentId
                ? new[] { (r.FirstParentId, r.SecondParentId) }
                : new[] { (r.FirstParentId, r.SecondParentId), (r.SecondParentId, r.FirstParentId) })
            .GroupBy(x => x.Item1).ToDictionary(g => g.Key,
                g => (IReadOnlyList<int>)Array.AsReadOnly(g.Select(x => x.Item2).Order().ToArray()));

        void ValidateRoot(int id)
        {
            if (!familyRoots.TryGetValue(id, out var root) || root != id)
                throw new InvalidDataException($"Nursery outcome is not a family root: {id}");
        }
    }

    public NurseryRecipe? Find(int firstSpeciesId, int secondSpeciesId) =>
        byPair.GetValueOrDefault(Key(firstSpeciesId, secondSpeciesId));

    // Species lookup alone cannot prove that equal-species parents are distinct individuals.
    public NurseryRecipe? Find(PokemonIndividual first, PokemonIndividual second) =>
        first.Id == second.Id ? null : Find(first.CurrentSpeciesId, second.CurrentSpeciesId);

    public IReadOnlyList<int> Partners(int speciesId) => partners.GetValueOrDefault(speciesId) ?? [];
    public int FamilyRoot(int speciesId) => familyRoots.GetValueOrDefault(speciesId);
    private static (int, int) Key(int a, int b) => a <= b ? (a, b) : (b, a);
}
