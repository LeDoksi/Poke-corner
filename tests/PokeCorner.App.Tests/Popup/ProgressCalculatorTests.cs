using PokeCorner.App.Popup;
using PokeCorner.Core.Activity;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Tests.Popup;

public class ProgressCalculatorTests
{
    private static PokemonIndividual MakeIndividual(int carePoints, int chainLength = 3) => new()
    {
        SpeciesId = 1,
        EvolutionChainId = 1,
        ChainLength = chainLength,
        Nature = Nature.Hardy,
        IsShiny = false,
        CaughtAtUtc = DateTimeOffset.UtcNow,
        AccumulatedCarePoints = carePoints
    };

    [Fact]
    public void Compute_CatchFraction_ReflectsUnitsSinceLastCheck()
    {
        var activity = new ActivityAccumulator();
        activity.AddUnits(ActivityAccumulator.CatchCheckIntervalUnits / 2);

        var result = ProgressCalculator.Compute(activity, active: null, new FeedingInventory());

        Assert.Equal(0.5, result.CatchFraction, precision: 3);
    }

    [Fact]
    public void Compute_EvolutionFraction_NoActiveIndividual_IsZero()
    {
        var result = ProgressCalculator.Compute(new ActivityAccumulator(), active: null, new FeedingInventory());

        // "Nothing to report", NOT a terminal state — no individual means no claim about one.
        Assert.Equal(EvolutionProgressState.InProgress, result.EvolutionState);
        Assert.Equal(0.0, result.EvolutionFraction!.Value);
    }

    [Fact]
    public void Compute_EvolutionFraction_Stage1_ReflectsProgressTowardStage2Threshold()
    {
        var individual = MakeIndividual(EvolutionThresholds.Stage2Threshold / 2);

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), individual, new FeedingInventory());

        Assert.Equal(EvolutionProgressState.InProgress, result.EvolutionState);
        Assert.Equal(0.5, result.EvolutionFraction!.Value, precision: 3);
    }

    [Fact]
    public void Compute_EvolutionFraction_Stage2_ReflectsProgressBetweenStage2AndStage3Thresholds()
    {
        var halfway = EvolutionThresholds.Stage2Threshold + (EvolutionThresholds.Stage3Threshold - EvolutionThresholds.Stage2Threshold) / 2;
        var individual = MakeIndividual(halfway);
        individual.CurrentStage = EvolutionStage.Stage2;

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), individual, new FeedingInventory());

        Assert.Equal(EvolutionProgressState.InProgress, result.EvolutionState);
        Assert.Equal(0.5, result.EvolutionFraction!.Value, precision: 3);
    }

    /// Regression-proofs the ordinary case: a real, mid-chain individual must still report a
    /// genuine 0-99% and must NOT be swept into either terminal state by the Part A fix.
    [Fact]
    public void Compute_EvolutionFraction_TwoStageChainAtStage1_IsGenuineInProgressFraction()
    {
        var individual = MakeIndividual(EvolutionThresholds.Stage2Threshold / 4, chainLength: 2);

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), individual, new FeedingInventory());

        Assert.Equal(EvolutionProgressState.InProgress, result.EvolutionState);
        Assert.Equal(0.25, result.EvolutionFraction!.Value, precision: 3);
        Assert.InRange(result.EvolutionFraction!.Value, 0.0, 0.999);
        Assert.Equal("25%", EvolutionProgressDisplay.Label(result.EvolutionFraction, result.EvolutionState));
    }

    /// Stage3 of a 3-long chain: evolved as far as it can go. Full bar, but the label must say
    /// "final form", not "100%" — the latter reads as "about to evolve any second".
    [Fact]
    public void Compute_EvolutionFraction_FinalStage_IsFinalFormNotAPercentage()
    {
        var individual = MakeIndividual(EvolutionThresholds.Stage3Threshold, chainLength: 3);
        individual.CurrentStage = EvolutionStage.Stage3;

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), individual, new FeedingInventory());

        Assert.Equal(EvolutionProgressState.FinalForm, result.EvolutionState);
        Assert.Null(result.EvolutionFraction);
        Assert.Equal(1.0, EvolutionProgressDisplay.BarValue(result.EvolutionFraction, result.EvolutionState));
        Assert.Equal(EvolutionProgressDisplay.FinalFormLabel, EvolutionProgressDisplay.Label(result.EvolutionFraction, result.EvolutionState));
    }

    /// Stage2 of a 2-long chain — the OTHER way to hit this chain's ceiling. Same signal as
    /// Stage3, and distinct from "never had evolutions".
    [Fact]
    public void Compute_EvolutionFraction_TwoStageChainAtStage2_IsFinalForm()
    {
        var individual = MakeIndividual(EvolutionThresholds.Stage3Threshold, chainLength: 2);
        individual.CurrentStage = EvolutionStage.Stage2;

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), individual, new FeedingInventory());

        Assert.Equal(EvolutionProgressState.FinalForm, result.EvolutionState);
        Assert.Null(result.EvolutionFraction);
        Assert.Equal(EvolutionProgressDisplay.FinalFormLabel, EvolutionProgressDisplay.Label(result.EvolutionFraction, result.EvolutionState));
    }

    /// The Emolga case from the live screenshot: ChainLength == 1, freshly caught. Must read
    /// "never evolves" with an EMPTY bar — it achieved nothing and can achieve nothing.
    [Fact]
    public void Compute_EvolutionFraction_SingleStageChain_NeverEvolvesAndBarIsEmpty()
    {
        var individual = MakeIndividual(0, chainLength: 1);

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), individual, new FeedingInventory());

        Assert.Equal(EvolutionProgressState.NeverEvolves, result.EvolutionState);
        Assert.Null(result.EvolutionFraction);
        Assert.Equal(0.0, EvolutionProgressDisplay.BarValue(result.EvolutionFraction, result.EvolutionState));
        Assert.Equal(EvolutionProgressDisplay.NeverEvolvesLabel, EvolutionProgressDisplay.Label(result.EvolutionFraction, result.EvolutionState));
    }

    /// A single-stage species stays "never evolves" no matter how much CP it banks — the old
    /// code let a high-CP Emolga fall through to a glowing full bar.
    [Fact]
    public void Compute_EvolutionFraction_SingleStageChain_StaysNeverEvolvesAtHighCarePoints()
    {
        var individual = MakeIndividual(EvolutionThresholds.Stage3Threshold * 10, chainLength: 1);

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), individual, new FeedingInventory());

        Assert.Equal(EvolutionProgressState.NeverEvolves, result.EvolutionState);
        Assert.Null(result.EvolutionFraction);
    }

    /// The two terminal states must not be interchangeable — that distinction IS the fix.
    [Fact]
    public void EvolutionProgressDisplay_NeverEvolvesAndFinalForm_AreDistinctLabelsAndBars()
    {
        Assert.NotEqual(EvolutionProgressDisplay.NeverEvolvesLabel, EvolutionProgressDisplay.FinalFormLabel);
        Assert.NotEqual(
            EvolutionProgressDisplay.BarValue(null, EvolutionProgressState.NeverEvolves),
            EvolutionProgressDisplay.BarValue(null, EvolutionProgressState.FinalForm));
    }

    [Fact]
    public void Compute_TreatFraction_ReflectsRemainderUnits()
    {
        var feeding = new FeedingInventory { RemainderUnits = FeedingService.ActivityUnitsPerTreatEarned / 4 };

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), active: null, feeding);

        Assert.Equal(0.25, result.TreatFraction, precision: 3);
    }

    [Fact]
    public void ComputeCoinFraction_ReflectsRemainderUnits()
    {
        var shop = new ShopInventory { RemainderUnits = ShopService.ActivityUnitsPerCoinEarned / 4 };

        var result = ProgressCalculator.ComputeCoinFraction(shop);

        Assert.Equal(0.25, result, precision: 3);
    }
}
