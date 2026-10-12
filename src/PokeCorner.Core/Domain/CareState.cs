namespace PokeCorner.Core.Domain;

public sealed class CareState
{
    public DateTimeOffset LastInteractionUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RanAwayUntilUtc { get; set; }

    /// Legacy checkpoint retained for compatible history decoding.
    public DateTimeOffset? LastNeglectCheckUtc { get; set; }

    /// Separate from LastInteractionUtc (which also covers Feed/Select and feeds the neglect
    /// timer) so FeedingService.Pet's spam-click cooldown doesn't get reset by those.
    public DateTimeOffset? LastPetUtc { get; set; }
}
