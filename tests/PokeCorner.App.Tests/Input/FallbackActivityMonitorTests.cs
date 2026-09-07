using PokeCorner.App.Input;
using Xunit;

namespace PokeCorner.App.Tests.Input;

public class FallbackActivityMonitorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void HookActive_OsRecent_DoesNotCredit()
    {
        // Hook recorded 1 second ago — well within HookStaleThreshold — so it's working
        // normally and the fallback must stay dormant, not double-count.
        var lastHookRecordedUtc = Now - TimeSpan.FromSeconds(1);

        Assert.False(FallbackActivityMonitor.ShouldCreditFallbackUnit(Now, lastHookRecordedUtc, TimeSpan.FromSeconds(0.5)));
    }

    [Fact]
    public void HookSilent_OsActive_Credits()
    {
        // Hook hasn't fired in 10 seconds (mhyprot2-style suppression), but the OS says the
        // player was at the mouse/keyboard half a second ago — exactly the Genshin scenario.
        var lastHookRecordedUtc = Now - TimeSpan.FromSeconds(10);

        Assert.True(FallbackActivityMonitor.ShouldCreditFallbackUnit(Now, lastHookRecordedUtc, TimeSpan.FromSeconds(0.5)));
    }

    [Fact]
    public void HookSilent_OsAlsoIdle_DoesNotCredit()
    {
        // Hook is silent AND the player is genuinely away from the PC — must not manufacture
        // activity out of nothing.
        var lastHookRecordedUtc = Now - TimeSpan.FromSeconds(10);

        Assert.False(FallbackActivityMonitor.ShouldCreditFallbackUnit(Now, lastHookRecordedUtc, TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void HookActive_OsAlsoIdle_DoesNotCredit()
    {
        var lastHookRecordedUtc = Now - TimeSpan.FromSeconds(1);

        Assert.False(FallbackActivityMonitor.ShouldCreditFallbackUnit(Now, lastHookRecordedUtc, TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void ExactlyAtStaleThreshold_CountsAsStale()
    {
        var lastHookRecordedUtc = Now - FallbackActivityMonitor.HookStaleThreshold;

        Assert.True(FallbackActivityMonitor.ShouldCreditFallbackUnit(Now, lastHookRecordedUtc, TimeSpan.Zero));
    }
}
