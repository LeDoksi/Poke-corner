using PokeCorner.Core.Persistence;
namespace PokeCorner.Core.Domain;

public enum EvolutionStage { Stage1 = 0, Stage2 = 1, Stage3 = 2 }

public sealed class PokemonIndividual
{
    public int CurrentSpeciesId { get; set; }
    public int FamilyRootId { get; set; }
    public LegacyOrigin? LegacyOrigin { get; set; }
    public int TotalXp { get; set; }
    public RationalRemainder XpRemainder { get; set; } = new();
    public string? FirstTraitId { get; set; }
    public string? SecondTraitId { get; set; }
    public string AcquisitionSource { get; set; } = "unknown";
    public List<Guid> ParentIds { get; set; } = [];
    public List<int> ReachedSpeciesIds { get; set; } = [];
    public Guid Id { get; init; } = Guid.NewGuid();
    public required int SpeciesId { get; init; }
    private int _evolutionChainId;

    /// The individual's evolution chain, fixed at catch time. Backed by a field purely so
    /// <see cref="RemapEvolutionChainId"/> can exist — an init-only accessor cannot be called
    /// from an instance method, and the legacy-offline-pool migration has to rewrite this
    /// value in place on load (see <see cref="LegacyOfflineChains"/>).
    public required int EvolutionChainId
    {
        get => _evolutionChainId;
        init => _evolutionChainId = value;
    }

    /// The ONLY mutation path for <see cref="EvolutionChainId"/>, and deliberately internal:
    /// nothing outside Core should be rewriting an individual's chain, and the one caller is
    /// the save-load migration in <see cref="LegacyOfflineChains.TryMigrate"/>.
    internal void RemapEvolutionChainId(int chainId) => _evolutionChainId = chainId;
    public required int ChainLength { get; init; }
    public required Nature Nature { get; init; }
    public required bool IsShiny { get; init; }
    public required DateTimeOffset CaughtAtUtc { get; init; }
    public int AccumulatedCarePoints { get; set; }
    public EvolutionStage CurrentStage { get; set; } = EvolutionStage.Stage1;

    /// Player-pinned "favorite" flag. Mutable (not init-only) because it is toggled from the
    /// roster long after the individual was created, and a plain public setter is all
    /// System.Text.Json's reflection-based (de)serializer needs to round-trip it through the
    /// save file — no schema-migration code required, older saves simply deserialize to false.
    public bool IsFavorite { get; set; }

    /// Cosmetic-only override of which reached evolution stage is DISPLAYED (sprite, card
    /// thumbnail) — 0-based, same indexing as <see cref="CurrentStage"/>. Never touches actual
    /// progress. null means "show CurrentStage", the default for every existing save. Must
    /// never exceed CurrentStage — enforced at the write site (AppState.SetDisplayStage); read
    /// sites still clamp/ignore an out-of-range value defensively, the same trust-boundary
    /// reasoning as Nickname's length clamp, since a hand-edited save file could set anything.
    public int? DisplayStageOverride { get; set; }

    /// Longest nickname accepted. The detail-overlay TextBox carries the same limit as a
    /// MaxLength, but the clamp below is what actually enforces it — a hand-edited save file
    /// is a trust boundary too, and a 5000-character "nickname" would wreck every card layout.
    public const int MaxNicknameLength = 20;

    private string? _nickname;

    /// Player-given name, shown ALONGSIDE the species name — never instead of it. Mutable for
    /// the same reason <see cref="IsFavorite"/> is: it is set long after the catch, and a plain
    /// public setter is all System.Text.Json needs to round-trip it through the save file.
    ///
    /// Normalized IN THE SETTER rather than at the call sites, so "  " and "" can never be
    /// stored as if they were a real nickname regardless of where the value came from — the
    /// detail overlay, a future rename flow, or a deserialized save. null is the one and only
    /// representation of "no nickname", which keeps every display site to a single null check.
    public string? Nickname
    {
        get => _nickname;
        set
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed)) { _nickname = null; return; }
            _nickname = trimmed.Length > MaxNicknameLength ? trimmed[..MaxNicknameLength] : trimmed;
        }
    }
}


public sealed class LegacyOrigin
{
    public int SpeciesId { get; set; }
    public int ChainId { get; set; }
    public int ChainLength { get; set; }
    public EvolutionStage CurrentStage { get; set; }
    public string LookupSha256 { get; set; } = "";
}
