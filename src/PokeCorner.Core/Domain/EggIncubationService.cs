namespace PokeCorner.Core.Domain;

public sealed class EggIncubationService
{
    public static readonly TimeSpan HatchDuration = TimeSpan.FromHours(1);

    public PendingEgg StartIncubating(RarityTier tier, DateTimeOffset nowUtc) =>
        new() { Tier = tier, ReadyAtUtc = nowUtc + HatchDuration };

    public bool IsReady(PendingEgg egg, DateTimeOffset nowUtc) => nowUtc >= egg.ReadyAtUtc;
}
