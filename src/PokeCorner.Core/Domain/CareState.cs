namespace PokeCorner.Core.Domain;

public sealed class CareState
{
    public DateTimeOffset LastInteractionUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RanAwayUntilUtc { get; set; }

    /// When the neglect poll last actually ran for this individual. Null until the first poll.
    /// A large gap between this and "now" means nothing was watching — the PC sat idle
    /// overnight, or the app was closed — which CareModeService.DiscountIdleGap uses to keep
    /// that unwatched time from counting as neglect.
    public DateTimeOffset? LastNeglectCheckUtc { get; set; }
}
