using PokeCorner.Core.Persistence;

namespace PokeCorner.Core.Domain;

public static class AchievementMetrics
{
    public static long? Get(SaveData state, string key) => state.Metrics.GetValueOrDefault(key);

    public static void Record(SaveData state, string key) => state.Metrics[key] = 1;

    public static void Refresh(SaveData state)
    {
        foreach (var individual in state.Individuals)
        {
            state.DiscoveredSpeciesIds.UnionWith(individual.ReachedSpeciesIds);
            if (individual.IsShiny) state.ShinySpeciesIds.UnionWith(individual.ReachedSpeciesIds);
            else state.NormalSpeciesIds.UnionWith(individual.ReachedSpeciesIds);
        }
        foreach (var encounter in state.Encounters.Where(e => e.Phase == "opened" && e.Individual is not null))
        {
            state.DiscoveredSpeciesIds.UnionWith(encounter.Individual!.ReachedSpeciesIds);
            if (encounter.Individual.IsShiny) state.ShinySpeciesIds.UnionWith(encounter.Individual.ReachedSpeciesIds);
            else state.NormalSpeciesIds.UnionWith(encounter.Individual.ReachedSpeciesIds);
        }
        foreach (var child in state.HatchedNurseryResults.Values)
        {
            state.DiscoveredSpeciesIds.UnionWith(child.ReachedSpeciesIds);
            if (child.IsShiny) state.ShinySpeciesIds.UnionWith(child.ReachedSpeciesIds);
            else state.NormalSpeciesIds.UnionWith(child.ReachedSpeciesIds);
        }

        var openedCatches = state.Encounters.Count(e => e.Phase == "opened" && e.Source is "activity" or "expedition");
        Max(state, "successful-catches", openedCatches);
        Max(state, "distinct-hatched-root-species", state.HatchedNurseryResults.Values.Select(x => x.SpeciesId).Distinct().Count());
        Max(state, "distinct-cared-individuals", state.Metrics.Keys.Count(k => k.StartsWith("care:", StringComparison.Ordinal)));
        foreach (var berry in SaveMigration.BerryIds)
            if (state.Berries.GetValueOrDefault(berry) > 0) Record(state, "berry-obtained:" + berry);
        if (state.Individuals.Any(i => EvolutionCalculator.StageForPoints(i.AccumulatedCarePoints, i.ChainLength) > i.CurrentStage))
            Record(state, "first-care-evolution-ready");
    }

    private static void Max(SaveData state, string key, int value)
    {
        if (value > 0 && value > (state.Metrics.GetValueOrDefault(key) ?? 0)) state.Metrics[key] = value;
    }
}
