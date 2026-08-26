using PokeCorner.App.Popup;
using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.App.Tests.Popup;

public class CollectionViewModelTests
{
    private static PokemonIndividual MakeIndividual(int speciesId = 1, int? evolutionChainId = null) => new()
    {
        SpeciesId = speciesId,
        EvolutionChainId = evolutionChainId ?? speciesId,
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
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

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
        var vm = new CollectionViewModel(collection, careStates, new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

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
        careModeService.TriggerRunAway(careState);
        var careStates = new Dictionary<Guid, CareState> { [individual.Id] = careState };
        var vm = new CollectionViewModel(collection, careStates, careModeService, CareMode.Hardcore, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

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
        careModeService.TriggerRunAway(careState);
        var careStates = new Dictionary<Guid, CareState> { [second.Id] = careState };
        var vm = new CollectionViewModel(collection, careStates, careModeService, CareMode.Hardcore, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

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
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

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
        var vm = new CollectionViewModel(collection, careStates, new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

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
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

        var released = vm.Release(individual.Id);

        Assert.True(released);
        Assert.Empty(collection.Individuals);
    }

    [Fact]
    public void GetEntries_IncludesRarityFromChainCaptureRate()
    {
        var collection = new PokemonCollection();
        var individual = MakeIndividual(evolutionChainId: 1);
        collection.Add(individual);
        var chains = new[] { new SpeciesEvolutionChain(ChainId: 1, StarterSpeciesId: individual.SpeciesId, CaptureRate: 255, ChainLength: 3, StageSpeciesIds: new[] { individual.SpeciesId }, StageSpeciesNames: new[] { "Testmon" }) };
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, chains, new List<PendingEgg>());

        var entries = vm.GetEntries(DateTimeOffset.UtcNow);

        Assert.Equal(RarityTier.Common, entries.Single().Rarity);
    }

    /// The Pokédex grid used to dedupe by species and sit above a separate per-individual
    /// roster. Both lists are now one per-individual grid, so GetEntries — its only source —
    /// must NOT collapse two owned copies of the same species into one card.
    [Fact]
    public void GetEntries_TwoIndividualsOfTheSameSpecies_AreTwoSeparateEntries()
    {
        var collection = new PokemonCollection();
        const int chainId = 1;
        const int speciesId = 10;
        collection.Add(MakeIndividual(evolutionChainId: chainId, speciesId: speciesId));
        collection.Add(MakeIndividual(evolutionChainId: chainId, speciesId: speciesId));
        var chains = new[] { new SpeciesEvolutionChain(ChainId: chainId, StarterSpeciesId: speciesId, CaptureRate: 100, ChainLength: 3, StageSpeciesIds: new[] { speciesId }, StageSpeciesNames: new[] { "Caterpie" }) };
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, chains, new List<PendingEgg>());

        var entries = vm.GetEntries(DateTimeOffset.UtcNow);

        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.Equal(speciesId, entry.DisplaySpeciesId));
        // Two cards, two distinct identities — the detail popup is addressed by individual id.
        Assert.Equal(2, entries.Select(entry => entry.IndividualId).Distinct().Count());

        // ...but the "Открыто видов" completion stat the view derives from these entries still
        // counts DISTINCT SPECIES, so a duplicate catch does not inflate it.
        Assert.Equal(1, entries.Select(entry => entry.DisplaySpeciesId).Distinct().Count());
    }

    /// Two individuals from the same chain at different stages are genuinely two different
    /// species on screen, and the distinct-species stat counts them as two.
    [Fact]
    public void GetEntries_SameChainAtDifferentStages_ExposesEachStageSeparately()
    {
        var collection = new PokemonCollection();
        var baby = MakeIndividual(speciesId: 4, evolutionChainId: 4);
        var evolved = MakeIndividual(speciesId: 4, evolutionChainId: 4);
        evolved.CurrentStage = EvolutionStage.Stage2;
        collection.Add(baby);
        collection.Add(evolved);
        var chains = new[] { new SpeciesEvolutionChain(ChainId: 4, StarterSpeciesId: 4, CaptureRate: 45, ChainLength: 3, StageSpeciesIds: new[] { 4, 5, 6 }, StageSpeciesNames: new[] { "Charmander", "Charmeleon", "Charizard" }) };
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, chains, new List<PendingEgg>());

        var entries = vm.GetEntries(DateTimeOffset.UtcNow);

        Assert.Equal(new[] { 4, 5 }, entries.Select(entry => entry.DisplaySpeciesId).OrderBy(id => id));
        Assert.Equal(new[] { "Charmander", "Charmeleon" }, entries.Select(entry => entry.SpeciesName).OrderBy(name => name));
    }

    [Fact]
    public void GetEntries_EvolvedIndividual_ExposesCurrentStageAsDisplaySpeciesId()
    {
        var collection = new PokemonCollection();
        var individual = MakeIndividual(speciesId: 4, evolutionChainId: 4);
        individual.CurrentStage = EvolutionStage.Stage2;
        collection.Add(individual);
        var chains = new[] { new SpeciesEvolutionChain(ChainId: 4, StarterSpeciesId: 4, CaptureRate: 45, ChainLength: 3, StageSpeciesIds: new[] { 4, 5, 6 }, StageSpeciesNames: new[] { "Charmander", "Charmeleon", "Charizard" }) };
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, chains, new List<PendingEgg>());

        var entry = vm.GetEntries(DateTimeOffset.UtcNow).Single();

        Assert.Equal(4, entry.SpeciesId);
        Assert.Equal(5, entry.DisplaySpeciesId);
    }

    [Fact]
    public void GetEntries_ExposesCaughtAtAndNatureForCardMetadata()
    {
        var collection = new PokemonCollection();
        var caughtAt = DateTimeOffset.UtcNow - TimeSpan.FromDays(3);
        var individual = new PokemonIndividual
        {
            SpeciesId = 1,
            EvolutionChainId = 1,
            ChainLength = 3,
            Nature = Nature.Jolly,
            IsShiny = false,
            CaughtAtUtc = caughtAt
        };
        collection.Add(individual);
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

        var entry = vm.GetEntries(DateTimeOffset.UtcNow).Single();

        Assert.Equal(caughtAt, entry.CaughtAtUtc);
        Assert.Equal(Nature.Jolly, entry.Nature);
    }

    [Fact]
    public void GetEntries_NoIndividualsOwned_IsEmpty()
    {
        var vm = new CollectionViewModel(new PokemonCollection(), new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

        Assert.Empty(vm.GetEntries(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void GetEntries_FavoriteCaughtLast_StillSortsFirst()
    {
        var collection = new PokemonCollection();
        var first = MakeIndividual(1);
        var second = MakeIndividual(2);
        var third = MakeIndividual(3);
        collection.Add(first);
        collection.Add(second);
        collection.Add(third);
        third.IsFavorite = true; // caught last, pinned
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

        var entries = vm.GetEntries(DateTimeOffset.UtcNow);

        Assert.Equal(third.Id, entries[0].IndividualId);
        Assert.True(entries[0].IsFavorite);
    }

    [Fact]
    public void GetEntries_FavoritesFirstButOtherwiseStable()
    {
        var collection = new PokemonCollection();
        var a = MakeIndividual(1);
        var b = MakeIndividual(2);
        var c = MakeIndividual(3);
        var d = MakeIndividual(4);
        collection.Add(a);
        collection.Add(b);
        collection.Add(c);
        collection.Add(d);
        b.IsFavorite = true;
        d.IsFavorite = true;
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

        var ids = vm.GetEntries(DateTimeOffset.UtcNow).Select(e => e.IndividualId).ToList();

        // Favorites float up keeping their own relative order (b before d), and the
        // non-favorites keep theirs too (a before c) — a stable sort, not a reshuffle.
        Assert.Equal(new[] { b.Id, d.Id, a.Id, c.Id }, ids);
    }

    [Fact]
    public void GetEntries_NoFavorites_PreservesOriginalOrder()
    {
        var collection = new PokemonCollection();
        var first = MakeIndividual(1);
        var second = MakeIndividual(2);
        collection.Add(first);
        collection.Add(second);
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

        var ids = vm.GetEntries(DateTimeOffset.UtcNow).Select(e => e.IndividualId).ToList();

        Assert.Equal(new[] { first.Id, second.Id }, ids);
    }

    [Fact]
    public void ToggleFavorite_FlipsFlagOnTheIndividualAndIsReflectedInEntries()
    {
        var collection = new PokemonCollection();
        var individual = MakeIndividual(1);
        collection.Add(individual);
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

        Assert.True(vm.ToggleFavorite(individual.Id));
        Assert.True(individual.IsFavorite);
        Assert.True(vm.GetEntries(DateTimeOffset.UtcNow).Single().IsFavorite);

        Assert.True(vm.ToggleFavorite(individual.Id));
        Assert.False(individual.IsFavorite);
        Assert.False(vm.GetEntries(DateTimeOffset.UtcNow).Single().IsFavorite);
    }

    [Fact]
    public void ToggleFavorite_UnknownId_ReturnsFalse()
    {
        var collection = new PokemonCollection();
        collection.Add(MakeIndividual(1));
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

        Assert.False(vm.ToggleFavorite(Guid.NewGuid()));
    }

    [Fact]
    public void GetPendingEggEntries_NotYetReady_HatchFractionBetweenZeroAndOneAndNotReady()
    {
        var now = DateTimeOffset.UtcNow;
        var egg = new PendingEgg { Tier = RarityTier.Rare, ReadyAtUtc = now + TimeSpan.FromMinutes(30) }; // half-incubated
        var vm = new CollectionViewModel(new PokemonCollection(), new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg> { egg });

        var entries = vm.GetPendingEggEntries(now);

        Assert.Equal(egg.Id, entries.Single().EggId);
        Assert.False(entries.Single().IsReady);
        Assert.InRange(entries.Single().HatchFraction, 0.45, 0.55);
    }

    [Fact]
    public void GetPendingEggEntries_PastReadyTime_IsReady()
    {
        var now = DateTimeOffset.UtcNow;
        var egg = new PendingEgg { Tier = RarityTier.Common, ReadyAtUtc = now - TimeSpan.FromMinutes(1) };
        var vm = new CollectionViewModel(new PokemonCollection(), new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg> { egg });

        var entries = vm.GetPendingEggEntries(now);

        Assert.True(entries.Single().IsReady);
        Assert.Equal(1.0, entries.Single().HatchFraction);
    }
}
