using PokeCorner.App.Popup;
using PokeCorner.Core.Activity;
using PokeCorner.Core.Domain;
using Xunit;

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

        Assert.Equal(0.0, result.EvolutionFraction);
    }

    [Fact]
    public void Compute_EvolutionFraction_Stage1_ReflectsProgressTowardStage2Threshold()
    {
        var individual = MakeIndividual(EvolutionThresholds.Stage2Threshold / 2);

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), individual, new FeedingInventory());

        Assert.Equal(0.5, result.EvolutionFraction, precision: 3);
    }

    [Fact]
    public void Compute_EvolutionFraction_Stage2_ReflectsProgressBetweenStage2AndStage3Thresholds()
    {
        var halfway = EvolutionThresholds.Stage2Threshold + (EvolutionThresholds.Stage3Threshold - EvolutionThresholds.Stage2Threshold) / 2;
        var individual = MakeIndividual(halfway);
        individual.CurrentStage = EvolutionStage.Stage2;

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), individual, new FeedingInventory());

        Assert.Equal(0.5, result.EvolutionFraction, precision: 3);
    }

    [Fact]
    public void Compute_EvolutionFraction_FinalStage_IsFull()
    {
        var individual = MakeIndividual(EvolutionThresholds.Stage3Threshold, chainLength: 3);
        individual.CurrentStage = EvolutionStage.Stage3;

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), individual, new FeedingInventory());

        Assert.Equal(1.0, result.EvolutionFraction);
    }

    [Fact]
    public void Compute_EvolutionFraction_SingleStageChain_IsAlwaysFull()
    {
        var individual = MakeIndividual(0, chainLength: 1);

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), individual, new FeedingInventory());

        Assert.Equal(1.0, result.EvolutionFraction);
    }

    [Fact]
    public void Compute_TreatFraction_ReflectsRemainderUnits()
    {
        var feeding = new FeedingInventory { RemainderUnits = FeedingService.ActivityUnitsPerTreatEarned / 4 };

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), active: null, feeding);

        Assert.Equal(0.25, result.TreatFraction, precision: 3);
    }
}
