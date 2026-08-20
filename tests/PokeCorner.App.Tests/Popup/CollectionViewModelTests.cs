using PokeCorner.App.Popup;
using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.App.Tests.Popup;

public class CollectionViewModelTests
{
    private static PokemonIndividual MakeIndividual(int speciesId) => new()
    {
        SpeciesId = speciesId,
        EvolutionChainId = speciesId,
        ChainLength = 3,
        Nature = Nature.Hardy,
        IsShiny = false,
        CaughtAtUtc = DateTimeOffset.UtcNow
    };

    [Fact]
    public void GetEntries_MarksActiveIndividualCorrectly()
    {
        var collection = new PokemonCollection();
        var first = MakeIndividual(1);
        var second = MakeIndividual(2);
        collection.Add(first);
        collection.Add(second);
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill);

        var entries = vm.GetEntries(DateTimeOffset.UtcNow);

        Assert.True(entries.Single(e => e.IndividualId == first.Id).IsActive);
        Assert.False(entries.Single(e => e.IndividualId == second.Id).IsActive);
    }

    [Fact]
    public void GetEntries_ChillMode_AlwaysAvailableToActivate()
    {
        var collection = new PokemonCollection();
        var individual = MakeIndividual(1);
        collection.Add(individual);
        var careStates = new Dictionary<Guid, CareState> { [individual.Id] = new CareState { LastInteractionUtc = DateTimeOffset.UtcNow - TimeSpan.FromDays(5) } };
        var vm = new CollectionViewModel(collection, careStates, new CareModeService(), CareMode.Chill);

        var entry = vm.GetEntries(DateTimeOffset.UtcNow).Single();

        Assert.True(entry.IsAvailableToActivate);
    }

    [Fact]
    public void GetEntries_HardcoreModeRanAway_NotAvailableUntilCooldownEnds()
    {
        var collection = new PokemonCollection();
        var individual = MakeIndividual(1);
        collection.Add(individual);
        var now = DateTimeOffset.UtcNow;
        var careState = new CareState();
        var careModeService = new CareModeService();
        careModeService.TriggerRunAway(careState, now);
        var careStates = new Dictionary<Guid, CareState> { [individual.Id] = careState };
        var vm = new CollectionViewModel(collection, careStates, careModeService, CareMode.Hardcore);

        var entry = vm.GetEntries(now).Single();

        Assert.False(entry.IsAvailableToActivate);
        Assert.Equal(careState.RanAwayUntilUtc, entry.AvailableAgainAtUtc);
    }

    [Fact]
    public void TrySetActive_UnavailableIndividual_ReturnsFalseAndDoesNotChangeActive()
    {
        var collection = new PokemonCollection();
        var first = MakeIndividual(1);
        var second = MakeIndividual(2);
        collection.Add(first);
        collection.Add(second);
        var now = DateTimeOffset.UtcNow;
        var careState = new CareState();
        var careModeService = new CareModeService();
        careModeService.TriggerRunAway(careState, now);
        var careStates = new Dictionary<Guid, CareState> { [second.Id] = careState };
        var vm = new CollectionViewModel(collection, careStates, careModeService, CareMode.Hardcore);

        var result = vm.TrySetActive(second.Id, now);

        Assert.False(result);
        Assert.Equal(first.Id, collection.ActiveIndividualId);
    }

    [Fact]
    public void TrySetActive_AvailableIndividual_ReturnsTrueAndSwitchesActive()
    {
        var collection = new PokemonCollection();
        var first = MakeIndividual(1);
        var second = MakeIndividual(2);
        collection.Add(first);
        collection.Add(second);
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill);

        var result = vm.TrySetActive(second.Id, DateTimeOffset.UtcNow);

        Assert.True(result);
        Assert.Equal(second.Id, collection.ActiveIndividualId);
    }

    [Fact]
    public void TrySetActive_Success_RegistersInteractionResettingNeglectTimer()
    {
        var collection = new PokemonCollection();
        var individual = MakeIndividual(1);
        collection.Add(individual);
        var nowUtc = DateTimeOffset.UtcNow;
        var careState = new CareState { LastInteractionUtc = nowUtc - TimeSpan.FromDays(2) };
        var careStates = new Dictionary<Guid, CareState> { [individual.Id] = careState };
        var vm = new CollectionViewModel(collection, careStates, new CareModeService(), CareMode.Chill);

        var result = vm.TrySetActive(individual.Id, nowUtc);

        Assert.True(result);
        Assert.Equal(nowUtc, careState.LastInteractionUtc);
    }

    [Fact]
    public void Release_DelegatesToCollection()
    {
        var collection = new PokemonCollection();
        var individual = MakeIndividual(1);
        collection.Add(individual);
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill);

        var released = vm.Release(individual.Id);

        Assert.True(released);
        Assert.Empty(collection.Individuals);
    }
}
