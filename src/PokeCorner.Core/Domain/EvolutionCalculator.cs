using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Domain;

public static class EvolutionThresholds
{
    // Relative to ActivityAccumulator.CatchCheckIntervalUnits (22_500): the first evolution must
    // cost noticeably MORE activity than a catch (~3.1 catches); the third ~8.7 catches
    // (cut 30% from 280_000 on 2026-10-02 — it felt like too long a grind).
    public const int Stage2Threshold = 70_000;
    public const int Stage3Threshold = 196_000;
}

public static class EvolutionCalculator
{
    public static int RequiredCareForNext(EvolutionStage currentStage) => currentStage switch
    {
        EvolutionStage.Stage1 => EvolutionThresholds.Stage2Threshold,
        EvolutionStage.Stage2 => EvolutionThresholds.Stage3Threshold,
        _ => int.MaxValue
    };

    /// Canonical cumulative care threshold. The trait is personal (unique slots), not a team bonus.
    public static int RequiredCareForNext(PokemonIndividual individual, TraitRules traits, LevelProgression levels,
        CatalogPackage catalog, EvolutionStage? stage = null)
    {
        var required = RequiredCareForNext(stage ?? individual.CurrentStage);
        if (required == int.MaxValue || individual.FirstTraitId != "fast-grower" && individual.SecondTraitId != "fast-grower"
            || !traits.Allowed(individual.CurrentSpeciesId, individual.IsShiny, catalog).Contains("fast-grower")) return required;
        var effect = TraitRules.Stack([traits.Value("fast-grower", levels.TraitTierForXp(individual.TotalXp))]);
        // Accepted thresholds and tier values produce integral care requirements.
        return decimal.ToInt32(required * (1m - effect));
    }

    public static EvolutionStage StageForPoints(int accumulatedCarePoints, int chainLength)
    {
        if (chainLength >= 3 && accumulatedCarePoints >= EvolutionThresholds.Stage3Threshold)
        {
            return EvolutionStage.Stage3;
        }
        if (chainLength >= 2 && accumulatedCarePoints >= EvolutionThresholds.Stage2Threshold)
        {
            return EvolutionStage.Stage2;
        }
        return EvolutionStage.Stage1;
    }
}
