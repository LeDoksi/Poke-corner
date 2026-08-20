using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.Core.Tests.Domain;

public class EggIncubationServiceTests
{
    [Fact]
    public void StartIncubating_SetsReadyAtUtcOneHourLater()
    {
        var service = new EggIncubationService();
        var now = DateTimeOffset.UtcNow;

        var egg = service.StartIncubating(RarityTier.Rare, now);

        Assert.Equal(RarityTier.Rare, egg.Tier);
        Assert.Equal(now + TimeSpan.FromHours(1), egg.ReadyAtUtc);
    }

    [Fact]
    public void IsReady_BeforeReadyTime_ReturnsFalse()
    {
        var service = new EggIncubationService();
        var now = DateTimeOffset.UtcNow;
        var egg = service.StartIncubating(RarityTier.Common, now);

        Assert.False(service.IsReady(egg, now + TimeSpan.FromMinutes(59)));
    }

    [Fact]
    public void IsReady_AtOrAfterReadyTime_ReturnsTrue()
    {
        var service = new EggIncubationService();
        var now = DateTimeOffset.UtcNow;
        var egg = service.StartIncubating(RarityTier.Common, now);

        Assert.True(service.IsReady(egg, now + TimeSpan.FromHours(1)));
    }
}
