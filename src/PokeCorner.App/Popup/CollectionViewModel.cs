using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

public sealed record CollectionEntry(
    Guid IndividualId,
    int SpeciesId,
    bool IsShiny,
    bool IsActive,
    bool IsAvailableToActivate,
    DateTimeOffset? AvailableAgainAtUtc);

public sealed class CollectionViewModel
{
    private readonly PokemonCollection _collection;
    private readonly IDictionary<Guid, CareState> _careStates;
    private readonly CareModeService _careModeService;
    private readonly CareMode _careMode;

    public CollectionViewModel(PokemonCollection collection, IDictionary<Guid, CareState> careStates, CareModeService careModeService, CareMode careMode)
    {
        _collection = collection;
        _careStates = careStates;
        _careModeService = careModeService;
        _careMode = careMode;
    }

    public IReadOnlyList<CollectionEntry> GetEntries(DateTimeOffset nowUtc) =>
        _collection.Individuals.Select(individual =>
        {
            var careState = _careStates.TryGetValue(individual.Id, out var state) ? state : new CareState();
            var isAvailable = _careMode != CareMode.Hardcore || _careModeService.IsAvailableToPlaceOnDesktop(careState, nowUtc);
            return new CollectionEntry(
                individual.Id,
                individual.SpeciesId,
                individual.IsShiny,
                IsActive: individual.Id == _collection.ActiveIndividualId,
                IsAvailableToActivate: isAvailable,
                AvailableAgainAtUtc: isAvailable ? null : careState.RanAwayUntilUtc);
        }).ToList();

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
