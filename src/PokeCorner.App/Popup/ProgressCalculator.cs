using PokeCorner.Core.Activity;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

public readonly record struct ProgressInfo(double CatchFraction, double EvolutionFraction, double TreatFraction);

public static class ProgressCalculator
{
    public static ProgressInfo Compute(ActivityAccumulator activity, PokemonIndividual? active, FeedingInventory feeding)
    {
        var catchFraction = Math.Clamp((double)activity.UnitsSinceLastCatchCheck / ActivityAccumulator.CatchCheckIntervalUnits, 0.0, 1.0);
        var treatFraction = Math.Clamp((double)feeding.RemainderUnits / FeedingService.ActivityUnitsPerTreatEarned, 0.0, 1.0);
        var evolutionFraction = active is null ? 0.0 : ComputeEvolutionFraction(active);

        return new ProgressInfo(catchFraction, evolutionFraction, treatFraction);
    }

    private static double ComputeEvolutionFraction(PokemonIndividual individual)
    {
        if (individual.CurrentStage == EvolutionStage.Stage1 && individual.ChainLength >= 2)
        {
            return Math.Clamp((double)individual.AccumulatedCarePoints / EvolutionThresholds.Stage2Threshold, 0.0, 1.0);
        }
        if (individual.CurrentStage == EvolutionStage.Stage2 && individual.ChainLength >= 3)
        {
            var span = EvolutionThresholds.Stage3Threshold - EvolutionThresholds.Stage2Threshold;
            var progress = individual.AccumulatedCarePoints - EvolutionThresholds.Stage2Threshold;
            return Math.Clamp((double)progress / span, 0.0, 1.0);
        }
        // Final stage for this chain length — nowhere further to progress.
        return 1.0;
    }
}
