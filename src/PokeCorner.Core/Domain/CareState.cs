namespace PokeCorner.Core.Domain;

public sealed class CareState
{
    public DateTimeOffset LastInteractionUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RanAwayUntilUtc { get; set; }
}
