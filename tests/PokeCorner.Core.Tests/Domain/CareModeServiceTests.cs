using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.Core.Tests.Domain;

public class CareModeServiceTests
{
    [Fact]
    public void ComputeMood_NoElapsedTime_IsFull()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now };

        Assert.Equal(1.0, service.ComputeMood(state, now), precision: 5);
    }

    [Fact]
    public void ComputeMood_HalfwayThroughDecayPeriod_IsHalf()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - (CareModeTuning.MoodDecayFullPeriod / 2) };

        Assert.Equal(0.5, service.ComputeMood(state, now), precision: 2);
    }

    [Fact]
    public void ComputeMood_PastFullDecayPeriod_ClampsAtZero()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - CareModeTuning.MoodDecayFullPeriod - TimeSpan.FromDays(1) };

        Assert.Equal(0.0, service.ComputeMood(state, now));
    }

    [Fact]
    public void ShouldRunAway_ChillMode_NeverTrue()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - TimeSpan.FromDays(10) };

        Assert.False(service.ShouldRunAway(state, now, CareMode.Chill));
    }

    [Fact]
    public void ShouldRunAway_HardcoreMode_BeforeThreshold_False()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - CareModeTuning.HardcoreNeglectThreshold + TimeSpan.FromMinutes(1) };

        Assert.False(service.ShouldRunAway(state, now, CareMode.Hardcore));
    }

    [Fact]
    public void ShouldRunAway_HardcoreMode_AfterThreshold_True()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - CareModeTuning.HardcoreNeglectThreshold - TimeSpan.FromMinutes(1) };

        Assert.True(service.ShouldRunAway(state, now, CareMode.Hardcore));
    }

    [Fact]
    public void TriggerRunAway_SetsOneHourCooldown()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now };

        service.TriggerRunAway(state, now);

        Assert.False(service.IsAvailableToPlaceOnDesktop(state, now));
        Assert.True(service.IsAvailableToPlaceOnDesktop(state, now + CareModeTuning.HardcoreRunAwayCooldown));
    }

    [Fact]
    public void RegisterInteraction_ClearsRunAwayCooldownAndResetsTimer()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - TimeSpan.FromHours(10) };
        service.TriggerRunAway(state, now);

        service.RegisterInteraction(state, now);

        Assert.True(service.IsAvailableToPlaceOnDesktop(state, now));
        Assert.Equal(now, state.LastInteractionUtc);
    }
}
