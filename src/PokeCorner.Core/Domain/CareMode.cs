namespace PokeCorner.Core.Domain;

public enum CareMode { Chill, Hardcore }

public static class CareModeTuning
{
    public static readonly TimeSpan MoodDecayFullPeriod = TimeSpan.FromHours(12);
}
