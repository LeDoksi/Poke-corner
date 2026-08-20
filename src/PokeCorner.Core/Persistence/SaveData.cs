using PokeCorner.Core.Domain;

namespace PokeCorner.Core.Persistence;

public sealed class SaveData
{
    public required List<PokemonIndividual> Individuals { get; init; }
    public Guid? ActiveIndividualId { get; init; }
    public required AppSettings Settings { get; init; }
    public int FeedingTreats { get; init; }
    public int ShopCoins { get; init; }
    public long ShopRemainderUnits { get; init; }
    public bool ShopCatchLuckyCharmActive { get; init; }
    public Dictionary<Guid, CareState> CareStates { get; init; } = new();
    public List<PendingEgg> PendingEggs { get; init; } = new();
}
