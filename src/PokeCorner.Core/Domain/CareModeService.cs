namespace PokeCorner.Core.Domain;

public sealed class CareModeService
{
    public double ComputeMood(CareState state, DateTimeOffset nowUtc)
    {
        var elapsedSeconds = (nowUtc - state.LastInteractionUtc).TotalSeconds;
        var fraction = elapsedSeconds / CareModeTuning.MoodDecayFullPeriod.TotalSeconds;
        return Math.Clamp(1.0 - fraction, 0.0, 1.0);
    }

    public void RegisterInteraction(CareState state, DateTimeOffset nowUtc)
    {
        state.LastInteractionUtc = nowUtc;
    }
}
