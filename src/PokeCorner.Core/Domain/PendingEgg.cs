namespace PokeCorner.Core.Domain;

public sealed class PendingEgg
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required RarityTier Tier { get; init; }
    public required DateTimeOffset ReadyAtUtc { get; init; }
}
