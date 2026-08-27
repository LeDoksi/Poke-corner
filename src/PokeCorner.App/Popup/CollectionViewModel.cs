using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

/// <param name="SpeciesId">The individual's permanently-fixed starter species.</param>
/// <param name="DisplaySpeciesId">The species id of the individual's CURRENT evolution
/// stage. This is what sprite thumbnails and Pokédex numbers must use, so an evolved
/// Pokémon's card shows Charizard rather than its baby Charmander form.</param>
public sealed record CollectionEntry(
    Guid IndividualId,
    int SpeciesId,
    int DisplaySpeciesId,
    string SpeciesName,
    bool IsShiny,
    bool IsActive,
    bool IsAvailableToActivate,
    DateTimeOffset? AvailableAgainAtUtc,
    RarityTier Rarity,
    double? EvolutionFraction,
    EvolutionProgressState EvolutionState,
    DateTimeOffset CaughtAtUtc,
    Nature Nature,
    bool IsFavorite,
    /// PokeAPI type names for the CURRENT stage's species — one for most, two for some.
    /// Empty when the catalog in use predates type capture.
    IReadOnlyList<string> Types,
    /// The individual's whole evolution line, spoiler-masked (see EvolutionStagePreview).
    /// Empty for a single-stage species: there is no line to preview.
    IReadOnlyList<EvolutionStagePreview> EvolutionStages);

/// One node of the evolution-chain strip in the detail view.
///
/// SPOILER-SAFE BY CONSTRUCTION: for a stage the player's individual has not reached,
/// <see cref="SpeciesId"/> and <see cref="SpeciesName"/> are null and <see cref="Types"/> is
/// empty — the identity is not merely hidden by the template, it is never put into the model
/// at all, so there is nothing to leak through a tooltip, a binding or the visual tree.
public sealed record EvolutionStagePreview(
    int StageIndex,
    int? SpeciesId,
    string? SpeciesName,
    IReadOnlyList<string> Types,
    bool IsCurrent)
{
    /// Reached stages are revealed; everything past the individual's current stage is a
    /// silhouette. Derived from the presence of the identity rather than stored separately, so
    /// the two can never contradict each other.
    public bool IsRevealed => SpeciesId is not null;
}

public sealed record PendingEggEntry(Guid EggId, RarityTier Tier, double HatchFraction, bool IsReady);

public sealed class CollectionViewModel
{
    private readonly PokemonCollection _collection;
    private readonly IDictionary<Guid, CareState> _careStates;
    private readonly CareModeService _careModeService;
    private readonly CareMode _careMode;
    private readonly IReadOnlyList<SpeciesEvolutionChain> _chains;
    private readonly IReadOnlyList<PendingEgg> _pendingEggs;

    public CollectionViewModel(PokemonCollection collection, IDictionary<Guid, CareState> careStates, CareModeService careModeService, CareMode careMode, IReadOnlyList<SpeciesEvolutionChain> chains, IReadOnlyList<PendingEgg> pendingEggs)
    {
        _collection = collection;
        _careStates = careStates;
        _careModeService = careModeService;
        _careMode = careMode;
        _chains = chains;
        _pendingEggs = pendingEggs;
    }

    /// Favorites float to the top. LINQ's OrderByDescending is a documented STABLE sort, so
    /// this only lifts the pinned individuals above the rest — relative catch order is
    /// preserved both among favorites and among everything else.
    public IReadOnlyList<CollectionEntry> GetEntries(DateTimeOffset nowUtc) =>
        _collection.Individuals.OrderByDescending(i => i.IsFavorite).Select(individual =>
        {
            var careState = _careStates.TryGetValue(individual.Id, out var state) ? state : new CareState();
            var isAvailable = _careMode != CareMode.Hardcore || _careModeService.IsAvailableToPlaceOnDesktop(careState, nowUtc);
            var displaySpeciesId = ResolveDisplaySpeciesId(individual);
            var progress = ProgressCalculator.Compute(new Core.Activity.ActivityAccumulator(), individual, new FeedingInventory());
            return new CollectionEntry(
                individual.Id,
                individual.SpeciesId,
                displaySpeciesId,
                _chains.ResolveSpeciesName(displaySpeciesId),
                individual.IsShiny,
                IsActive: individual.Id == _collection.ActiveIndividualId,
                IsAvailableToActivate: isAvailable,
                AvailableAgainAtUtc: isAvailable ? null : careState.RanAwayUntilUtc,
                Rarity: RarityForChain(individual.EvolutionChainId),
                EvolutionFraction: progress.EvolutionFraction,
                EvolutionState: progress.EvolutionState,
                CaughtAtUtc: individual.CaughtAtUtc,
                Nature: individual.Nature,
                IsFavorite: individual.IsFavorite,
                Types: _chains.ResolveSpeciesTypes(displaySpeciesId),
                EvolutionStages: BuildEvolutionPreview(individual));
        }).ToList();

    /// Builds the spoiler-masked evolution strip for one individual: every stage its chain
    /// has, with the identity filled in only up to the stage this individual has reached.
    ///
    /// Returns EMPTY for a single-stage species (and for a chain missing from the catalog) —
    /// a one-node "chain" with no arrows would be a confusing thing to draw, so the section
    /// simply doesn't render.
    private IReadOnlyList<EvolutionStagePreview> BuildEvolutionPreview(PokemonIndividual individual)
    {
        var chain = _chains.FirstOrDefault(c => c.ChainId == individual.EvolutionChainId);
        if (chain is null || chain.ChainLength <= 1) return Array.Empty<EvolutionStagePreview>();

        var reachedIndex = (int)individual.CurrentStage;
        var stages = new List<EvolutionStagePreview>(chain.ChainLength);

        for (var index = 0; index < chain.ChainLength; index++)
        {
            // Revealed only up to and including the stage actually reached. The bounds check
            // also covers a legacy chain whose ChainLength outruns its stage id list.
            var isRevealed = index <= reachedIndex && index < chain.StageSpeciesIds.Count;
            int? speciesId = isRevealed ? chain.StageSpeciesIds[index] : null;

            stages.Add(new EvolutionStagePreview(
                StageIndex: index,
                SpeciesId: speciesId,
                SpeciesName: speciesId is null ? null : _chains.ResolveSpeciesName(speciesId.Value),
                Types: speciesId is null ? Array.Empty<string>() : _chains.ResolveSpeciesTypes(speciesId.Value),
                IsCurrent: index == reachedIndex));
        }

        return stages;
    }

    // (GetPokedexEntries lived here: one deduped entry per distinct DISPLAY species, feeding
    // a Pokédex grid that sat above a separate roster list of the same Pokémon. The two lists
    // were merged into one per-individual grid, so GetEntries above is now the only source
    // the collection screen reads and the deduped projection had no remaining caller. The
    // "Открыто видов: N" completion stat is still distinct-species — the view derives it with
    // a Distinct() over the same entries rather than a second pass over the collection.)

    /// Mirrors App.xaml.cs's ResolveDisplaySpeciesId: walk the individual's chain to the
    /// species id of its current stage, falling back to the fixed starter species when the
    /// chain isn't in the catalog or the stage index is out of range.
    private int ResolveDisplaySpeciesId(PokemonIndividual individual)
    {
        var chain = _chains.FirstOrDefault(c => c.ChainId == individual.EvolutionChainId);
        if (chain is null) return individual.SpeciesId;

        var stageIndex = (int)individual.CurrentStage;
        return stageIndex < chain.StageSpeciesIds.Count ? chain.StageSpeciesIds[stageIndex] : individual.SpeciesId;
    }

    public IReadOnlyList<PendingEggEntry> GetPendingEggEntries(DateTimeOffset nowUtc) =>
        _pendingEggs.Select(egg =>
        {
            var startedAtUtc = egg.ReadyAtUtc - EggIncubationService.HatchDuration;
            var elapsedSeconds = (nowUtc - startedAtUtc).TotalSeconds;
            var fraction = Math.Clamp(elapsedSeconds / EggIncubationService.HatchDuration.TotalSeconds, 0.0, 1.0);
            return new PendingEggEntry(egg.Id, egg.Tier, fraction, nowUtc >= egg.ReadyAtUtc);
        }).ToList();

    private RarityTier RarityForChain(int evolutionChainId)
    {
        var chain = _chains.FirstOrDefault(c => c.ChainId == evolutionChainId);
        return chain is null ? RarityTier.Common : RarityClassifier.Classify(chain.CaptureRate);
    }

    public bool TrySetActive(Guid id, DateTimeOffset nowUtc)
    {
        if (!_careStates.TryGetValue(id, out var careState))
        {
            careState = new CareState();
            _careStates[id] = careState;
        }
        var isAvailable = _careMode != CareMode.Hardcore || _careModeService.IsAvailableToPlaceOnDesktop(careState, nowUtc);
        if (!isAvailable) return false;

        _collection.SetActive(id);
        _careModeService.RegisterInteraction(careState, nowUtc);
        return true;
    }

    public bool Release(Guid id) => _collection.Release(id);

    /// Flips the pin on one individual and reports its new state. Returns false when the id
    /// isn't in the collection (e.g. a stale card clicked after a release).
    public bool ToggleFavorite(Guid id)
    {
        var individual = _collection.Individuals.FirstOrDefault(i => i.Id == id);
        if (individual is null) return false;

        individual.IsFavorite = !individual.IsFavorite;
        return true;
    }
}
