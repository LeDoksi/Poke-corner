using PokeCorner.Core.Domain;

namespace PokeCorner.Core.Persistence;

public sealed class SaveData
{
    public required List<PokemonIndividual> Individuals { get; init; }
    public Guid? ActiveIndividualId { get; init; }
    public required AppSettings Settings { get; init; }
    public int FeedingTreats { get; init; }

    /// Progress toward the next catch ("До поимки") and the next treat ("До лакомства"), so
    /// both bars survive a relaunch.
    public long CatchAccumulatorUnits { get; init; }
    public long FeedingRemainderUnits { get; init; }

    public int ShopCoins { get; init; }
    public long ShopRemainderUnits { get; init; }
    public bool ShopCatchLuckyCharmActive { get; init; }
    public Dictionary<Guid, CareState> CareStates { get; init; } = new();
    public List<PendingEgg> PendingEggs { get; init; } = new();

    /// How many catches are queued waiting for the player to claim them (capped at
    /// App.MaxPendingCatches).
    public int PendingCatchCount { get; init; }
}
