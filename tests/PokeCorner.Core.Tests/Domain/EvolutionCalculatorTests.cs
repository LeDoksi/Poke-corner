using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.Core.Tests.Domain;

public class EvolutionCalculatorTests
{
    [Theory]
    [InlineData(0, 3, EvolutionStage.Stage1)]
    [InlineData(37499, 3, EvolutionStage.Stage1)]
    [InlineData(37500, 3, EvolutionStage.Stage2)]
    [InlineData(149999, 3, EvolutionStage.Stage2)]
    [InlineData(150000, 3, EvolutionStage.Stage3)]
    [InlineData(999_999, 3, EvolutionStage.Stage3)]
    public void StageForPoints_ThreeStageChain_MatchesThresholds(int points, int chainLength, EvolutionStage expected)
    {
        Assert.Equal(expected, EvolutionCalculator.StageForPoints(points, chainLength));
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
