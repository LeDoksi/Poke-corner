namespace PokeCorner.Core.Domain;

public enum CareMode { Chill, Hardcore }

public static class CareModeTuning
{
    public static readonly TimeSpan MoodDecayFullPeriod = TimeSpan.FromHours(12);
    public static readonly TimeSpan HardcoreNeglectThreshold = TimeSpan.FromHours(6);
    public static readonly TimeSpan HardcoreRunAwayCooldown = TimeSpan.FromHours(1);
}
