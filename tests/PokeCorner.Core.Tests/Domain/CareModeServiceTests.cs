using PokeCorner.Core.Domain;

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

    /// Root-cause regression for the "6 hours of testing and nothing visibly happened" report:
    /// Hardcore mood must decay over the 6h HardcoreNeglectThreshold (the actual run-away
    /// deadline), not the 12h Chill-only MoodDecayFullPeriod — otherwise mood never reaches 0.0
    /// by the moment the pet actually runs away.
    [Fact]
    public void ComputeMood_HardcoreMode_HalfwayThroughNeglectThreshold_IsHalf()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - (CareModeTuning.HardcoreNeglectThreshold / 2) };

        Assert.Equal(0.5, service.ComputeMood(state, now, CareMode.Hardcore), precision: 2);
    }

    [Fact]
    public void ComputeMood_HardcoreMode_AtNeglectThreshold_IsZero()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - CareModeTuning.HardcoreNeglectThreshold };

        Assert.Equal(0.0, service.ComputeMood(state, now, CareMode.Hardcore), precision: 5);
    }

    /// Chill's calculation is unchanged — same 12h period whether or not the mode is passed
    /// explicitly, so a hypothetical future Chill caller keeps today's gentle ambient feel.
    [Fact]
    public void ComputeMood_ChillModeExplicit_MatchesDefaultTwelveHourPeriod()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - (CareModeTuning.MoodDecayFullPeriod / 2) };

        Assert.Equal(service.ComputeMood(state, now), service.ComputeMood(state, now, CareMode.Chill), precision: 5);
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

    /// A two-hour gap between polls means the PC was almost certainly idle (or the app closed).
    /// One hour of that is ordinary neglect and still counts; the other hour is discounted, so
    /// a pet that looked seven hours neglected is back at exactly the six-hour threshold.
    [Fact]
    public void DiscountIdleGap_LongGapBetweenPolls_NudgesNeglectClockForward()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState
        {
            LastInteractionUtc = now - TimeSpan.FromHours(7),
            LastNeglectCheckUtc = now - TimeSpan.FromHours(2)
        };

        service.DiscountIdleGap(state, now);

        Assert.Equal(now - TimeSpan.FromHours(6), state.LastInteractionUtc);
        Assert.Equal(now, state.LastNeglectCheckUtc);
    }

    [Fact]
    public void DiscountIdleGap_ShortGapBetweenPolls_LeavesNeglectClockAlone()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var lastInteraction = now - TimeSpan.FromHours(5);
        var state = new CareState
        {
            LastInteractionUtc = lastInteraction,
            LastNeglectCheckUtc = now - TimeSpan.FromSeconds(1)
        };

        service.DiscountIdleGap(state, now);

        Assert.Equal(lastInteraction, state.LastInteractionUtc);
    }

    [Fact]
    public void DiscountIdleGap_MissingCheckpoint_DiscountsUnobservedTimeAndRecordsPoll()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var lastInteraction = now - TimeSpan.FromHours(9);
        var state = new CareState { LastInteractionUtc = lastInteraction, LastNeglectCheckUtc = null };

        service.DiscountIdleGap(state, now);

        Assert.Equal(now - CareModeTuning.NeglectIdleGrace, state.LastInteractionUtc);
        Assert.Equal(now, state.LastNeglectCheckUtc);
        Assert.False(service.ShouldRunAway(state, now, CareMode.Hardcore));
    }

    [Fact]
    public void TriggerRunAway_SetsCooldownAnchoredToWhenNeglectBegan()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now };

        service.TriggerRunAway(state);

        var neglectBegins = now + CareModeTuning.HardcoreNeglectThreshold;
        Assert.Equal(neglectBegins + CareModeTuning.HardcoreRunAwayCooldown, state.RanAwayUntilUtc);
    }

    /// The neglect poll only runs while the app is open, so a threshold crossed hours ago
    /// first fires at the next launch. Back-dating the cooldown to the real neglect moment
    /// means that already-served time counts — the pet is immediately available again
    /// instead of being benched for a fresh hour from launch.
    [Fact]
    public void TriggerRunAway_NeglectStartedLongAgo_CooldownIsAlreadyExpired()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState
        {
            LastInteractionUtc = now - CareModeTuning.HardcoreNeglectThreshold - CareModeTuning.HardcoreRunAwayCooldown - TimeSpan.FromMinutes(1)
        };

        service.TriggerRunAway(state);

        Assert.True(state.RanAwayUntilUtc < now);
        Assert.True(service.IsAvailableToPlaceOnDesktop(state, now));
    }

    [Fact]
    public void RegisterInteraction_ClearsRunAwayCooldownAndResetsTimer()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - TimeSpan.FromHours(10) };
        service.TriggerRunAway(state);

        service.RegisterInteraction(state, now);

        Assert.True(service.IsAvailableToPlaceOnDesktop(state, now));
        Assert.Equal(now, state.LastInteractionUtc);
    }
}
