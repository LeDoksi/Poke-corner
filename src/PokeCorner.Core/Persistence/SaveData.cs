using PokeCorner.Core.Domain;

namespace PokeCorner.Core.Persistence;

public sealed class SaveData
{
    public required List<PokemonIndividual> Individuals { get; init; }
    public Guid? ActiveIndividualId { get; init; }
    public required AppSettings Settings { get; init; }
    public int FeedingTreats { get; init; }

    /// Progress toward the next catch ("До поимки") and the next treat ("До лакомства").
    /// Both were missing from this schema, so both bars silently reset to zero on every
    /// relaunch while evolution progress — stored per-individual as AccumulatedCarePoints,
    /// a different and correctly-persisted path — survived. ShopRemainderUnits below was
    /// always here, which is why the coin bar was the odd one out that already worked.
    public long CatchAccumulatorUnits { get; init; }
    public long FeedingRemainderUnits { get; init; }

    public int ShopCoins { get; init; }
    public long ShopRemainderUnits { get; init; }
    public bool ShopCatchLuckyCharmActive { get; init; }
    public Dictionary<Guid, CareState> CareStates { get; init; } = new();
    public List<PendingEgg> PendingEggs { get; init; } = new();
}
