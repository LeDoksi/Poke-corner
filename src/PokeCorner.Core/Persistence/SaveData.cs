using PokeCorner.Core.Domain;

namespace PokeCorner.Core.Persistence;

public sealed class SaveData
{
    public bool StarterSelectionPending { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public string? CatalogRevision { get; set; }
    public string? BalanceRevision { get; set; }
    public string? MigrationId { get; set; }
    public HashSet<int> DiscoveredSpeciesIds { get; set; } = [];
    public HashSet<string> EvolutionTransitions { get; set; } = [];
    public HashSet<int> ShinySpeciesIds { get; set; } = [];
    public Dictionary<string, long?> Metrics { get; set; } = [];
    public Dictionary<string, AwardEntry> AwardLedger { get; set; } = [];
    public Dictionary<string, int> Berries { get; set; } = [];
    public Dictionary<string, int> Items { get; set; } = [];
    public Dictionary<string, RationalRemainder> BerryRemainders { get; set; } = [];
    public RationalRemainder CoinRemainder { get; set; } = new();
    public TutorialState Tutorial { get; set; } = new();
    public GiftGrant Gift { get; set; } = new();
    public TimedActivity? Expedition { get; set; }
    public TimedActivity? Nest { get; set; }
    public Dictionary<string, Guid> CompletedOperations { get; set; } = [];
    public List<EncounterState> Encounters { get; set; } = [];
    public bool ActivityNewGuaranteed { get; set; }
    public required List<PokemonIndividual> Individuals { get; set; }
    public Guid? ActiveIndividualId { get; set; }
    public required AppSettings Settings { get; set; }
    public int FeedingTreats { get; set; }

    /// Progress toward the next catch ("До поимки") and the next treat ("До лакомства"), so
    /// both bars survive a relaunch.
    public long CatchAccumulatorUnits { get; set; }
    public long FeedingRemainderUnits { get; set; }

    public int ShopCoins { get; set; }
    public long ShopRemainderUnits { get; set; }
    public bool ShopCatchLuckyCharmActive { get; set; }
    public Dictionary<Guid, CareState> CareStates { get; set; } = new();
    public List<PendingEgg> PendingEggs { get; set; } = new();

    /// Earned activity balls waiting to be opened.
    public int PendingCatchCount { get; set; }
}


// IDs are shared with the shipped balance/asset manifest; counts start empty at migration.
public sealed class AwardEntry
{
    public DateTimeOffset AwardedAtUtc { get; set; }
    public string ClaimStatus { get; set; } = "claimed";
    public int CoinsGranted { get; set; }
    public string? CatalogRevision { get; set; }
    public List<int> AvailableSpeciesIds { get; set; } = [];
}

public sealed class TutorialState
{
    public string TutorialRevision { get; set; } = "global-update-v1";
    public string Status { get; set; } = "not-started";
    public int CurrentStep { get; set; } = 1;
    public HashSet<string> SeenHintKeys { get; set; } = [];
}

public sealed class GiftGrant
{
    public string GrantId { get; set; } = "global-update-compatible-pair-v1";
    public string? RecipeId { get; set; }
    public int? ExcludedSpeciesId { get; set; }
    public List<ReservedGift> Gifts { get; set; } = [];
}

public sealed class ReservedGift
{
    public string Status { get; set; } = "reserved";
    public required PokemonIndividual Individual { get; set; }
}

// Payload is captured by the dependent expedition/nest services, never recomputed at claim.
public sealed class TimedActivity
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "running";
    public List<Guid> ParticipantIds { get; set; } = [];
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset ReadyAtUtc { get; set; }
    public int DurationMinutes { get; set; }
    public Dictionary<string, int> ResourcePrices { get; set; } = [];
    public System.Text.Json.JsonElement Conditions { get; set; }
    public System.Text.Json.JsonElement Outcomes { get; set; }
}
