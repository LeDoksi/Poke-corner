using System.Text.Json;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Domain;

public sealed class NurseryCatalog
{
    // Species-level recipes as written in the data, and the family-level recipes the game uses (D-32).
    private readonly Dictionary<(int, int), NurseryRecipe> byPair = [];
    private readonly Dictionary<(int, int), NurseryRecipe> byFamily = [];
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

        var choiceExtraBerries = balance.RootElement.GetProperty("choiceExtraBerries").GetInt32();
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
        // D-32 (Georgy 04.11): a parent is an evolution family, not one stage of it. Every species-level
        // recipe between two families pools its babies; cost and time follow the rarest available baby, as before.
        foreach (var group in byPair.Values.GroupBy(r => Key(familyRoots[r.FirstParentId], familyRoots[r.SecondParentId])))
            byFamily[group.Key] = Merge(group.Key, group.ToArray());
        MixedRecipes = byFamily.Values.Where(r => r.FirstParentId != r.SecondParentId).ToArray();
        SameSpeciesRecipes = byFamily.Values.Where(r => r.FirstParentId == r.SecondParentId).ToArray();
        partners = byFamily.Values.Where(r => r.IsAvailable)
            .SelectMany(r => r.FirstParentId == r.SecondParentId
                ? new[] { (r.FirstParentId, r.SecondParentId) }
                : new[] { (r.FirstParentId, r.SecondParentId), (r.SecondParentId, r.FirstParentId) })
            .GroupBy(x => x.Item1).ToDictionary(g => g.Key,
                g => (IReadOnlyList<int>)Array.AsReadOnly(g.Select(x => x.Item2).Order().ToArray()));

        NurseryRecipe Merge((int First, int Second) family, NurseryRecipe[] parts)
        {
            // The root-and-root recipe (or the same-species recipe of the root) is the family's own price when it exists.
            int Rank(NurseryRecipe r) => r.Outcomes.Count == 0 ? -1 : r.Outcomes.Max(o => Array.IndexOf(rarityOrder, o.Rarity));
            var priced = parts
                .OrderByDescending(Rank)
                .ThenByDescending(r => r.FirstParentId == family.First && r.SecondParentId == family.Second
                    || r.FirstParentId == family.Second && r.SecondParentId == family.First)
                .ThenBy(r => r.FirstParentId).ThenBy(r => r.SecondParentId).First();
            static NurseryOutcome[] Union(IEnumerable<NurseryOutcome> outcomes) =>
                outcomes.GroupBy(o => o.SpeciesId).Select(g => g.First()).OrderBy(o => o.SpeciesId).ToArray();
            // A stage without art must not hide its family's babies: availability is judged per baby,
            // the pet actually put into the nest is checked on its own when it is picked.
            var outcomes = Union(parts.SelectMany(r => r.Outcomes));
            var available = outcomes.Where(o => catalog.CanObtain(o.SpeciesId, false)).ToArray();
            var surcharge = available.Length <= 1 ? new Dictionary<string, int>()
                : priced.SelectedSurcharge.Count > 0 ? new Dictionary<string, int>(priced.SelectedSurcharge)
                : parts.Where(r => r.SelectedSurcharge.Count > 0).Select(r => new Dictionary<string, int>(r.SelectedSurcharge)).FirstOrDefault()
                    ?? new Dictionary<string, int> { [priced.BaseBerries.OrderByDescending(x => x.Value).First().Key] = choiceExtraBerries };
            return new NurseryRecipe(family.First, family.Second, outcomes, available, priced.BaseBerries, surcharge,
                parts.Any(r => r.NeedsThemeReview), Hours(available));
        }

        void ValidateRoot(int id)
        {
            if (!familyRoots.TryGetValue(id, out var root) || root != id)
                throw new InvalidDataException($"Nursery outcome is not a family root: {id}");
        }
    }

    /// Any stage of either family: Venusaur stands in for Bulbasaur (D-32).
    public NurseryRecipe? Find(int firstSpeciesId, int secondSpeciesId) =>
        familyRoots.TryGetValue(firstSpeciesId, out var a) && familyRoots.TryGetValue(secondSpeciesId, out var b)
            ? byFamily.GetValueOrDefault(Key(a, b)) : null;

    // Species lookup alone cannot prove that equal-species parents are distinct individuals.
    public NurseryRecipe? Find(PokemonIndividual first, PokemonIndividual second) =>
        first.Id == second.Id ? null : Find(first.CurrentSpeciesId, second.CurrentSpeciesId);

    /// Partner families (their root species ids) of the family this species belongs to.
    public IReadOnlyList<int> Partners(int speciesId) => partners.GetValueOrDefault(FamilyRoot(speciesId)) ?? [];
    public int FamilyRoot(int speciesId) => familyRoots.GetValueOrDefault(speciesId);
    private static (int, int) Key(int a, int b) => a <= b ? (a, b) : (b, a);
}
