using PokeCorner.Core.Activity;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

/// Why an individual's evolution bar is not filling toward anything.
///
/// The bar used to be a bare 0..1 double, and BOTH terminal cases fell out of it as a flat
/// 1.0 — a freshly-caught Emolga (a species with no evolutions at all) glowed at "100%",
/// reading as "about to evolve any second". Two different situations hide behind that number
/// and neither is a percentage, so they are named here instead of being encoded as a value.
public enum EvolutionProgressState
{
    /// Genuinely progressing toward a next stage — EvolutionFraction carries a real 0..1.
    InProgress,

    /// ChainLength == 1: this species has no evolutions and never will. Nothing to progress
    /// toward, and nothing was ever achieved — the bar is empty, not full.
    NeverEvolves,

    /// The individual reached the last stage its own chain offers (Stage2 of a 2-long chain,
    /// or Stage3). Also nothing left to progress toward, but unlike NeverEvolves the player
    /// actually got it there, so a full bar is honest — only the "100%" label is not.
    FinalForm
}

public readonly record struct ProgressInfo(
    double CatchFraction,
    /// null in both terminal states — see EvolutionProgressState. Never null while InProgress.
    double? EvolutionFraction,
    double TreatFraction,
    EvolutionProgressState EvolutionState);

/// The single place that turns the three-way evolution state into what a bar and a label
/// should show. Shared so the "Прогресс" card, the Pokédex detail overlay and the desktop
/// mini-panel cannot drift into three different wordings of the same state.
public static class EvolutionProgressDisplay
{
    public const string NeverEvolvesLabel = "Не эволюционирует";
    public const string FinalFormLabel = "Финальная форма";

    /// Empty for NeverEvolves (there is no progress to depict), full for FinalForm (the
    /// individual really did evolve as far as it can), the real fraction otherwise.
    public static double BarValue(double? fraction, EvolutionProgressState state) => state switch
    {
        EvolutionProgressState.NeverEvolves => 0.0,
        EvolutionProgressState.FinalForm => 1.0,
        _ => Math.Clamp(fraction ?? 0.0, 0.0, 1.0)
    };

    public static string Label(double? fraction, EvolutionProgressState state) => state switch
    {
        EvolutionProgressState.NeverEvolves => NeverEvolvesLabel,
        EvolutionProgressState.FinalForm => FinalFormLabel,
        _ => $"{Math.Clamp(fraction ?? 0.0, 0.0, 1.0) * 100:0}%"
    };

    /// Longer wording for tooltips, where the bar itself carries no text (the desktop gauge).
    public static string ToolTip(EvolutionProgressState state) => state switch
    {
        EvolutionProgressState.NeverEvolves => "Этот покемон не имеет эволюций",
        EvolutionProgressState.FinalForm => "Достигнута финальная эволюция",
        _ => "Прогресс до следующей эволюции"
    };

    /// Muted rendering for the one state where a bar would otherwise imply progress that can
    /// never happen. FinalForm stays at full opacity — its full bar is earned.
    public static double BarOpacity(EvolutionProgressState state) =>
        state == EvolutionProgressState.NeverEvolves ? 0.35 : 1.0;
}

public static class ProgressCalculator
{
    public static ProgressInfo Compute(ActivityAccumulator activity, PokemonIndividual? active, FeedingInventory feeding)
    {
        var catchFraction = Math.Clamp((double)activity.UnitsSinceLastCatchCheck / ActivityAccumulator.CatchCheckIntervalUnits, 0.0, 1.0);
        var treatFraction = Math.Clamp((double)feeding.RemainderUnits / FeedingService.ActivityUnitsPerTreatEarned, 0.0, 1.0);

        // No active pet is not a terminal evolution state — it is "nothing to report", which
        // stays an ordinary empty in-progress bar rather than claiming anything about a
        // species that isn't there.
        var (evolutionFraction, evolutionState) = active is null
            ? ((double?)0.0, EvolutionProgressState.InProgress)
            : ComputeEvolutionFraction(active);

        return new ProgressInfo(catchFraction, evolutionFraction, treatFraction, evolutionState);
    }

    public static double ComputeCoinFraction(ShopInventory shop) =>
        Math.Clamp((double)shop.RemainderUnits / ShopService.ActivityUnitsPerCoinEarned, 0.0, 1.0);

    private static (double? Fraction, EvolutionProgressState State) ComputeEvolutionFraction(PokemonIndividual individual)
    {
        // Checked first and on ChainLength alone: a single-stage species is "never evolves"
        // from the moment it is caught, independently of which stage it claims to be on.
        if (individual.ChainLength <= 1)
        {
            return (null, EvolutionProgressState.NeverEvolves);
        }

        if (individual.CurrentStage == EvolutionStage.Stage1 && individual.ChainLength >= 2)
        {
            var fraction = Math.Clamp((double)individual.AccumulatedCarePoints / EvolutionThresholds.Stage2Threshold, 0.0, 1.0);
            return (fraction, EvolutionProgressState.InProgress);
        }
        if (individual.CurrentStage == EvolutionStage.Stage2 && individual.ChainLength >= 3)
        {
            var span = EvolutionThresholds.Stage3Threshold - EvolutionThresholds.Stage2Threshold;
            var progress = individual.AccumulatedCarePoints - EvolutionThresholds.Stage2Threshold;
            return (Math.Clamp((double)progress / span, 0.0, 1.0), EvolutionProgressState.InProgress);
        }

        // Reached this chain's ceiling: Stage2 of a 2-long chain, or Stage3. The individual
        // evolved as far as it can, so the bar is full — but it is a final form, not a 100%
        // countdown to something that will never arrive.
        return (null, EvolutionProgressState.FinalForm);
    }
}
