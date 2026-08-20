using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

public sealed record CollectionEntry(
    Guid IndividualId,
    int SpeciesId,
    string SpeciesName,
    bool IsShiny,
    bool IsActive,
    bool IsAvailableToActivate,
    DateTimeOffset? AvailableAgainAtUtc,
    RarityTier Rarity,
    double EvolutionFraction);

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

    public IReadOnlyList<CollectionEntry> GetEntries(DateTimeOffset nowUtc) =>
        _collection.Individuals.Select(individual =>
        {
            var careState = _careStates.TryGetValue(individual.Id, out var state) ? state : new CareState();
            var isAvailable = _careMode != CareMode.Hardcore || _careModeService.IsAvailableToPlaceOnDesktop(careState, nowUtc);
            return new CollectionEntry(
                individual.Id,
                individual.SpeciesId,
                SpeciesNames.GetName(individual.SpeciesId),
                individual.IsShiny,
                IsActive: individual.Id == _collection.ActiveIndividualId,
                IsAvailableToActivate: isAvailable,
                AvailableAgainAtUtc: isAvailable ? null : careState.RanAwayUntilUtc,
                Rarity: RarityForChain(individual.EvolutionChainId),
                EvolutionFraction: ProgressCalculator.Compute(new Core.Activity.ActivityAccumulator(), individual, new FeedingInventory()).EvolutionFraction);
        }).ToList();

    public IReadOnlyList<PokedexEntry> GetPokedexEntries() =>
        _collection.Individuals
            .Select(i => i.SpeciesId)
            .Distinct()
            .Select(speciesId =>
            {
                var owningIndividual = _collection.Individuals.First(i => i.SpeciesId == speciesId);
                return new PokedexEntry(speciesId, SpeciesNames.GetName(speciesId), RarityForChain(owningIndividual.EvolutionChainId));
            })
            .ToList();

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
}
