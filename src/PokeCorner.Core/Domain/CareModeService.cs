namespace PokeCorner.Core.Domain;

public sealed class CareModeService
{
    public double ComputeMood(CareState state, DateTimeOffset nowUtc)
    {
        var elapsedSeconds = (nowUtc - state.LastInteractionUtc).TotalSeconds;
        var fraction = elapsedSeconds / CareModeTuning.MoodDecayFullPeriod.TotalSeconds;
        return Math.Clamp(1.0 - fraction, 0.0, 1.0);
    }

    public bool ShouldRunAway(CareState state, DateTimeOffset nowUtc, CareMode mode)
    {
        if (mode != CareMode.Hardcore) return false;
        if (state.RanAwayUntilUtc is not null) return false;
        return nowUtc - state.LastInteractionUtc >= CareModeTuning.HardcoreNeglectThreshold;
    }

    public void TriggerRunAway(CareState state, DateTimeOffset nowUtc)
    {
        state.RanAwayUntilUtc = nowUtc + CareModeTuning.HardcoreRunAwayCooldown;
    }

    public bool IsAvailableToPlaceOnDesktop(CareState state, DateTimeOffset nowUtc) =>
        state.RanAwayUntilUtc is null || nowUtc >= state.RanAwayUntilUtc;

    public void RegisterInteraction(CareState state, DateTimeOffset nowUtc)
    {
        state.LastInteractionUtc = nowUtc;
        state.RanAwayUntilUtc = null;
    }
}
