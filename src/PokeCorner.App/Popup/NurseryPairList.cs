using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;

namespace PokeCorner.App.Popup;

/// One possible baby of a pair, with its chance among the pair's available outcomes.
public sealed record NurseryPairOutcome(int SpeciesId, int Percent, bool IsNew, bool ShinyAvailable);

/// A family pair the collection can put into the nest, with the pets the nursery would use for it.
/// FirstSpeciesId/SecondSpeciesId are the families' root species (D-32: any stage is a parent).
/// Busy is set when the collection owns the species but not enough free pets right now.
public sealed record NurseryPairOption(NurseryRecipe Recipe, Guid FirstId, Guid SecondId,
    IReadOnlyList<NurseryPairOutcome> Outcomes, string? Busy, int BusySpeciesId = 0)
{
    public int FirstSpeciesId => Recipe.FirstParentId;
    public int SecondSpeciesId => Recipe.SecondParentId;
    public bool IsSameSpecies => Recipe.FirstParentId == Recipe.SecondParentId;
    public int NewCount => Outcomes.Count(o => o.IsNew);
    public bool Matches(int a, int b) => (a == FirstSpeciesId && b == SecondSpeciesId) || (a == SecondSpeciesId && b == FirstSpeciesId);
}

/// "Книга пар" (Georgy 01.11): every pair the collection can breed right now, worked out from the recipes
/// instead of making the player try parents one by one. A species has only ~2 partner species, so a
/// collection of 90 species holds only a dozen or so pairs; the list stays short until the Pokédex is nearly full.
public static class NurseryPairList
{
    public static IReadOnlyList<NurseryPairOption> Build(SaveData data, NurseryCatalog recipes,
        Func<PokemonIndividual, string?> unavailable)
    {
        var bySpecies = data.Individuals.GroupBy(p => recipes.FamilyRoot(p.CurrentSpeciesId)).ToDictionary(g => g.Key, g => g.ToArray());
        var result = new List<NurseryPairOption>();
        foreach (var recipe in recipes.MixedRecipes.Concat(recipes.SameSpeciesRecipes))
        {
            if (!recipe.IsAvailable) continue;
            if (!bySpecies.TryGetValue(recipe.FirstParentId, out var firsts) ||
                !bySpecies.TryGetValue(recipe.SecondParentId, out var seconds)) continue;
            var same = recipe.FirstParentId == recipe.SecondParentId;
            if (same && firsts.Length < 2) continue;
            var freeFirst = Best(firsts, unavailable);
            var first = freeFirst.FirstOrDefault();
            var second = Best(seconds, unavailable).FirstOrDefault(p => first is null || p.Id != first.Id);
            string? busy = null;
            var busySpecies = 0;
            if (first is null || second is null)
            {
                var stuck = first is null ? firsts : seconds;
                busySpecies = first is null ? recipe.FirstParentId : recipe.SecondParentId;
                busy = stuck.Select(unavailable).FirstOrDefault(reason => reason is not null) ?? "Сейчас занят";
            }
            result.Add(new(recipe, first?.Id ?? Guid.Empty, second?.Id ?? Guid.Empty, Outcomes(data, recipe), busy, busySpecies));
        }
        return result;
    }

    /// Free pets of one species, best parent first: a caring parent shortens the wait, then the most grown.
    public static IEnumerable<PokemonIndividual> Best(IEnumerable<PokemonIndividual> pets, Func<PokemonIndividual, string?> unavailable) =>
        pets.Where(p => unavailable(p) is null)
            .OrderByDescending(p => p.FirstTraitId == "caring-parent" || p.SecondTraitId == "caring-parent")
            .ThenByDescending(p => p.TotalXp).ThenBy(p => p.CaughtAtUtc).ThenBy(p => p.Id);

    public static IReadOnlyList<NurseryPairOutcome> Outcomes(SaveData data, NurseryRecipe recipe)
    {
        var total = recipe.AvailableOutcomes.Sum(o => o.Weight);
        return recipe.AvailableOutcomes.Select(o => new NurseryPairOutcome(o.SpeciesId,
            total <= 0 ? 0 : (int)Math.Round(100.0 * o.Weight / total, MidpointRounding.AwayFromZero),
            !data.NormalSpeciesIds.Contains(o.SpeciesId), o.ShinyAvailable)).ToArray();
    }

    /// Pairs that can start now come first, those that can bring a new species before the rest.
    public static IEnumerable<NurseryPairOption> Ordered(IEnumerable<NurseryPairOption> pairs) =>
        pairs.OrderBy(p => p.Busy is not null).ThenByDescending(p => p.NewCount)
            .ThenBy(p => p.Recipe.Hours ?? int.MaxValue).ThenBy(p => p.FirstSpeciesId).ThenBy(p => p.SecondSpeciesId);
}
