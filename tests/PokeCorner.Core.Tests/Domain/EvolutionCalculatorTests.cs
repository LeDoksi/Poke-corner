using PokeCorner.Core.Activity;
using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.Core.Tests.Domain;

public class EvolutionCalculatorTests
{
    // Expressed against the constants rather than repeating their literal values: this Theory
    // previously hardcoded 37500/150000 and had to be edited by hand when the thresholds were
    // retuned, which is exactly the drift it is supposed to catch.
    [Theory]
    [InlineData(0, 3, EvolutionStage.Stage1)]
    [InlineData(EvolutionThresholds.Stage2Threshold - 1, 3, EvolutionStage.Stage1)]
    [InlineData(EvolutionThresholds.Stage2Threshold, 3, EvolutionStage.Stage2)]
    [InlineData(EvolutionThresholds.Stage3Threshold - 1, 3, EvolutionStage.Stage2)]
    [InlineData(EvolutionThresholds.Stage3Threshold, 3, EvolutionStage.Stage3)]
    [InlineData(int.MaxValue, 3, EvolutionStage.Stage3)]
    public void StageForPoints_ThreeStageChain_MatchesThresholds(int points, int chainLength, EvolutionStage expected)
    {
        Assert.Equal(expected, EvolutionCalculator.StageForPoints(points, chainLength));
    }

    /// The tuning requirement behind the thresholds, per live feedback: reaching the first
    /// evolution must cost more activity than catching three Pokémon. At the old 37_500 it was
    /// worth ~1.7 catches, so evolutions arrived faster than catches did.
    [Fact]
    public void Stage2Threshold_CostsAtLeastThreeCatchesWorthOfActivity()
    {
        Assert.True(EvolutionThresholds.Stage2Threshold >= 3 * ActivityAccumulator.CatchCheckIntervalUnits);
    }

    /// Per the 2026-10-02 decision (POKE-20): the third stage costs 30% less than the original
    /// 280_000 — i.e. 196_000 total, not 30% off the Stage2→Stage3 gap.
    [Fact]
    public void Stage3Threshold_Is30PercentBelowTheOriginal280k()
    {
        Assert.Equal(196_000, EvolutionThresholds.Stage3Threshold);
    }

    [Fact]
    public void StageForPoints_SingleStageChain_NeverEvolves()
    {
        Assert.Equal(EvolutionStage.Stage1, EvolutionCalculator.StageForPoints(999_999, chainLength: 1));
    }

    [Fact]
    public void StageForPoints_TwoStageChain_CapsAtStage2()
    {
        Assert.Equal(EvolutionStage.Stage2, EvolutionCalculator.StageForPoints(999_999, chainLength: 2));
    }
}
