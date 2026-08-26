namespace PokeCorner.Core.Domain;

public enum CareMode { Chill, Hardcore }

public static class CareModeTuning
{
    public static readonly TimeSpan MoodDecayFullPeriod = TimeSpan.FromHours(12);
    public static readonly TimeSpan HardcoreNeglectThreshold = TimeSpan.FromHours(6);
    public static readonly TimeSpan HardcoreRunAwayCooldown = TimeSpan.FromHours(1);

    /// How much of a gap between neglect polls still counts as ordinary neglect. Anything
    /// beyond this is treated as "nobody was at the machine" and discounted — see
    /// CareModeService.DiscountIdleGap.
    public static readonly TimeSpan NeglectIdleGrace = TimeSpan.FromHours(1);
}
