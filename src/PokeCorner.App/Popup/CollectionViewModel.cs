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
    double EvolutionFraction,
    DateTimeOffset CaughtAtUtc,
    Nature Nature,
    bool IsFavorite);

public sealed record PokedexEntry(int SpeciesId, string SpeciesName, RarityTier Rarity);

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
            return new CollectionEntry(
                individual.Id,
                individual.SpeciesId,
                displaySpeciesId,
                SpeciesNames.GetName(displaySpeciesId),
                individual.IsShiny,
                IsActive: individual.Id == _collection.ActiveIndividualId,
                IsAvailableToActivate: isAvailable,
                AvailableAgainAtUtc: isAvailable ? null : careState.RanAwayUntilUtc,
                Rarity: RarityForChain(individual.EvolutionChainId),
                EvolutionFraction: ProgressCalculator.Compute(new Core.Activity.ActivityAccumulator(), individual, new FeedingInventory()).EvolutionFraction,
                CaughtAtUtc: individual.CaughtAtUtc,
                Nature: individual.Nature,
                IsFavorite: individual.IsFavorite);
        }).ToList();

    /// Deduped by DISPLAY species, not by the individual's fixed starter species: a player
    /// who evolved Charmander into Charizard has genuinely "seen" both forms, and deduping
    /// on the starter id would collapse them into a single Charmander entry forever.
    public IReadOnlyList<PokedexEntry> GetPokedexEntries() =>
        _collection.Individuals
            .Select(individual => (SpeciesId: ResolveDisplaySpeciesId(individual), individual.EvolutionChainId))
            .GroupBy(pair => pair.SpeciesId)
            .Select(group => new PokedexEntry(
                group.Key,
                SpeciesNames.GetName(group.Key),
                RarityForChain(group.First().EvolutionChainId)))
            .ToList();

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
