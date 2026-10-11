namespace PokeCorner.Core.Domain;

public sealed class CareModeService
{
    /// mode: Chill (default) keeps mood decaying over the full 12h MoodDecayFullPeriod — Chill
    /// has no run-away consequence, so its gentle ambient feel is unchanged. Hardcore decays
    /// over the 6h HardcoreNeglectThreshold instead, the ACTUAL deadline before the pet runs
    /// away (see ShouldRunAway) — without this, mood was decoupled from that deadline and never
    /// reached 0.0 by the time the pet actually left (6h is only half of the 12h period), which
    /// made SetMoodTint's droop/dim barely perceptible right when it mattered most.
    public double ComputeMood(CareState state, DateTimeOffset nowUtc, CareMode mode = CareMode.Chill)
    {
        var fullPeriod = mode == CareMode.Hardcore ? CareModeTuning.HardcoreNeglectThreshold : CareModeTuning.MoodDecayFullPeriod;
        var elapsedSeconds = (nowUtc - state.LastInteractionUtc).TotalSeconds;
        var fraction = elapsedSeconds / fullPeriod.TotalSeconds;
        return Math.Clamp(1.0 - fraction, 0.0, 1.0);
    }

    /// Keeps time nobody was at the machine from counting as neglect, then records this poll.
    /// Call once per neglect poll, before ShouldRunAway.
    ///
    /// The neglect clock is just "how long since LastInteraction", which can't tell an ignored
    /// pet from a PC left on overnight or an app that was closed all weekend. Both show up the
    /// same way: a long gap between consecutive polls. Everything past the first hour of such a
    /// gap is pushed back out of the clock by nudging LastInteraction forward — the first hour
    /// still counts, so an ordinary coffee break isn't special-cased.
    ///
    /// The app-was-closed case needs no separate handling: no ticks ran, so LastNeglectCheckUtc
    /// stays stale and the first tick after relaunch sees (and discounts) the whole gap.
    ///
    /// This composes with TriggerRunAway's back-dating for free — that reads LastInteractionUtc,
    /// which is exactly the value adjusted here.
    public void DiscountIdleGap(CareState state, DateTimeOffset nowUtc)
    {
        // Legacy saves without a checkpoint cannot prove neglect while the app was closed.
        var lastCheckUtc = state.LastNeglectCheckUtc ?? state.LastInteractionUtc;
        var gap = nowUtc - lastCheckUtc;
        if (gap > CareModeTuning.NeglectIdleGrace)
        {
            state.LastInteractionUtc += gap - CareModeTuning.NeglectIdleGrace;
        }

        state.LastNeglectCheckUtc = nowUtc;
    }

    public bool ShouldRunAway(CareState state, DateTimeOffset nowUtc, CareMode mode)
    {
        if (mode != CareMode.Hardcore) return false;
        if (state.RanAwayUntilUtc is not null) return false;
        return nowUtc - state.LastInteractionUtc >= CareModeTuning.HardcoreNeglectThreshold;
    }

    /// The cooldown is anchored to the moment neglect actually began — LastInteraction plus
    /// the neglect threshold — NOT to "now". Nothing polls for neglect while the app is
    /// closed, so a threshold crossed hours ago only surfaces at the next launch; anchoring
    /// to that launch handed the player a fresh full hour of cooldown for neglect they had
    /// already long since served.
    public void TriggerRunAway(CareState state)
    {
        var neglectedSinceUtc = state.LastInteractionUtc + CareModeTuning.HardcoreNeglectThreshold;
        state.RanAwayUntilUtc = neglectedSinceUtc + CareModeTuning.HardcoreRunAwayCooldown;
    }

    public bool IsAvailableToPlaceOnDesktop(CareState state, DateTimeOffset nowUtc) =>
        state.RanAwayUntilUtc is null || nowUtc >= state.RanAwayUntilUtc;

    public void RegisterInteraction(CareState state, DateTimeOffset nowUtc)
    {
        state.LastInteractionUtc = nowUtc;
        state.RanAwayUntilUtc = null;
    }
}
