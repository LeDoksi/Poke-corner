using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.Core.Tests.Domain;

public class RarityClassifierTests
{
    [Theory]
    [InlineData(255, RarityTier.Common)]
    [InlineData(150, RarityTier.Common)]
    [InlineData(149, RarityTier.Uncommon)]
    [InlineData(70, RarityTier.Uncommon)]
    [InlineData(69, RarityTier.Rare)]
    [InlineData(30, RarityTier.Rare)]
    [InlineData(29, RarityTier.Legendary)]
    [InlineData(3, RarityTier.Legendary)]
    public void Classify_MapsCaptureRateToTier(int captureRate, RarityTier expected)
    {
        Assert.Equal(expected, RarityClassifier.Classify(captureRate));
    }
}
