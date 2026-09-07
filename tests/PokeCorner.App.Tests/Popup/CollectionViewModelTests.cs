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

    // ===================== Default grid order =====================
    // Three bands — the individual on the desktop, then the favorites, then the rest — with
    // dex number ordering inside each band. Catch order used to be the tie-breaker, which
    // scattered a large collection.

    [Fact]
    public void GetEntries_FavoriteCaughtLast_SortsAboveThePlainOnes()
    {
        var collection = new PokemonCollection();
        var active = MakeIndividual(1);   // first added => the active pet
        var plain = MakeIndividual(2);
        var pinned = MakeIndividual(3);
        collection.Add(active);
        collection.Add(plain);
        collection.Add(pinned);
        pinned.IsFavorite = true; // caught last, pinned
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

        var entries = vm.GetEntries(DateTimeOffset.UtcNow);

        // The active pet leads regardless; the pin lifts its individual past the plain one it
        // was caught after, even though the plain one has the lower dex number.
        Assert.Equal(new[] { active.Id, pinned.Id, plain.Id }, entries.Select(e => e.IndividualId));
        Assert.True(entries[1].IsFavorite);
    }

    [Fact]
    public void GetEntries_ActiveFirstThenFavoritesThenTheRest_EachBandByDexNumber()
    {
        var collection = new PokemonCollection();
        // Caught in an order that deliberately DISAGREES with dex order, so passing this test
        // requires the dex sort rather than the old catch-order fallback.
        var active = MakeIndividual(50);
        var plainHigh = MakeIndividual(40);
        var pinnedHigh = MakeIndividual(30);
        var plainLow = MakeIndividual(10);
        var pinnedLow = MakeIndividual(20);
        collection.Add(active);
        collection.Add(plainHigh);
        collection.Add(pinnedHigh);
        collection.Add(plainLow);
        collection.Add(pinnedLow);
        pinnedHigh.IsFavorite = true;
        pinnedLow.IsFavorite = true;
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

        var ids = vm.GetEntries(DateTimeOffset.UtcNow).Select(e => e.IndividualId).ToList();

        Assert.Equal(new[] { active.Id, pinnedLow.Id, pinnedHigh.Id, plainLow.Id, plainHigh.Id }, ids);
    }

    /// The dex number that sorts is the CURRENT stage's, not the fixed starter species — the
    /// same number the card actually prints — so an evolved Charmander sorts as Charmeleon.
    [Fact]
    public void GetEntries_SortsByTheCurrentStagesDexNumberNotTheStarterSpecies()
    {
        var collection = new PokemonCollection();
        var evolved = MakeIndividual(speciesId: 4, evolutionChainId: 4); // Charmander -> #5
        var pidgey = MakeIndividual(speciesId: 16, evolutionChainId: 16);
        var caterpie = MakeIndividual(speciesId: 10, evolutionChainId: 10);
        collection.Add(evolved);
        collection.Add(pidgey);
        collection.Add(caterpie);
        evolved.CurrentStage = EvolutionStage.Stage2;
        var chains = new[]
        {
            new SpeciesEvolutionChain(4, 4, 45, 3, new[] { 4, 5, 6 }, new[] { "Charmander", "Charmeleon", "Charizard" }),
            new SpeciesEvolutionChain(10, 10, 255, 3, new[] { 10, 11, 12 }, new[] { "Caterpie", "Metapod", "Butterfree" }),
            new SpeciesEvolutionChain(16, 16, 255, 3, new[] { 16, 17, 18 }, new[] { "Pidgey", "Pidgeotto", "Pidgeot" })
        };
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, chains, new List<PendingEgg>());

        // The evolved one is the active pet (added first), so the assertion that matters is the
        // pair behind it: #10 before #16.
        var entries = vm.GetEntries(DateTimeOffset.UtcNow);

        Assert.Equal(new[] { 5, 10, 16 }, entries.Select(e => e.DisplaySpeciesId));
    }

    [Fact]
    public void GetEntries_NoFavorites_OrdersByDexNumberBehindTheActivePet()
    {
        var collection = new PokemonCollection();
        var active = MakeIndividual(1);
        var high = MakeIndividual(90);
        var low = MakeIndividual(20);
        collection.Add(active);
        collection.Add(high);
        collection.Add(low);
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

        var ids = vm.GetEntries(DateTimeOffset.UtcNow).Select(e => e.IndividualId).ToList();

        Assert.Equal(new[] { active.Id, low.Id, high.Id }, ids);
    }

    /// The active pet outranks even a pinned one — it is the Pokémon the player is looking at
    /// on their desktop, so it must be the first card on the grid.
    [Fact]
    public void GetEntries_ActiveIndividual_LeadsEvenWhenItIsNotAFavorite()
    {
        var collection = new PokemonCollection();
        var pinned = MakeIndividual(2);
        var active = MakeIndividual(99);
        collection.Add(pinned);
        collection.Add(active);
        pinned.IsFavorite = true;
        collection.SetActive(active.Id);
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

        var entries = vm.GetEntries(DateTimeOffset.UtcNow);

        Assert.Equal(active.Id, entries[0].IndividualId);
        Assert.True(entries[0].IsActive);
        Assert.Equal(pinned.Id, entries[1].IndividualId);
    }

    // ===================== Nicknames =====================

    [Fact]
    public void SetNickname_ShowsUpOnTheEntryAlongsideTheSpeciesName()
    {
        var collection = new PokemonCollection();
        var individual = MakeIndividual(speciesId: 25, evolutionChainId: 25);
        collection.Add(individual);
        var chains = new[] { new SpeciesEvolutionChain(25, 25, 190, 2, new[] { 25, 26 }, new[] { "Pikachu", "Raichu" }) };
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, chains, new List<PendingEgg>());

        Assert.True(vm.SetNickname(individual.Id, "Sparky"));

        var entry = vm.GetEntries(DateTimeOffset.UtcNow).Single();
        Assert.Equal("Sparky", entry.Nickname);
        // The species name is still there — the nickname is additive, never a replacement.
        Assert.Equal("Pikachu", entry.SpeciesName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n ")]
    [InlineData(null)]
    public void SetNickname_EmptyOrWhitespace_IsTreatedAsNoNickname(string? blank)
    {
        var collection = new PokemonCollection();
        var individual = MakeIndividual(1);
        collection.Add(individual);
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());
        vm.SetNickname(individual.Id, "Sparky");

        vm.SetNickname(individual.Id, blank);

        Assert.Null(individual.Nickname);
        Assert.Null(vm.GetEntries(DateTimeOffset.UtcNow).Single().Nickname);
    }

    [Fact]
    public void SetNickname_TrimsSurroundingWhitespaceAndClampsToTheLengthLimit()
    {
        var collection = new PokemonCollection();
        var individual = MakeIndividual(1);
        collection.Add(individual);
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

        vm.SetNickname(individual.Id, "  Sparky  ");
        Assert.Equal("Sparky", individual.Nickname);

        vm.SetNickname(individual.Id, new string('x', PokemonIndividual.MaxNicknameLength + 10));
        Assert.Equal(PokemonIndividual.MaxNicknameLength, individual.Nickname!.Length);
    }

    [Fact]
    public void SetNickname_UnknownId_ReturnsFalse()
    {
        var collection = new PokemonCollection();
        collection.Add(MakeIndividual(1));
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

        Assert.False(vm.SetNickname(Guid.NewGuid(), "Sparky"));
    }

    // ===================== Search =====================

    private static CollectionEntry EntryFor(string speciesName, string? nickname) => new(
        IndividualId: Guid.NewGuid(), SpeciesId: 25, DisplaySpeciesId: 25, SpeciesName: speciesName,
        Nickname: nickname, IsShiny: false, IsActive: false, IsAvailableToActivate: true,
        AvailableAgainAtUtc: null, Rarity: RarityTier.Common, EvolutionFraction: null,
        EvolutionState: EvolutionProgressState.NeverEvolves, CaughtAtUtc: DateTimeOffset.UtcNow,
        Nature: Nature.Hardy, IsFavorite: false, Types: Array.Empty<string>(),
        EvolutionStages: Array.Empty<EvolutionStagePreview>(), HasEvolutionsRemaining: false);

    [Theory]
    [InlineData("pika", true)]   // substring of the species name
    [InlineData("PIKA", true)]   // case-insensitive
    [InlineData("chu", true)]    // matches mid-word, not just a prefix
    [InlineData("charm", false)]
    public void MatchesSearch_MatchesTheSpeciesNameCaseInsensitively(string query, bool expected) =>
        Assert.Equal(expected, CollectionViewModel.MatchesSearch(EntryFor("Pikachu", null), query));

    /// The whole point of item 4's last clause: a player who renamed their Pikachu must be able
    /// to find it by the name THEY gave it.
    [Theory]
    [InlineData("sparky", true)]
    [InlineData("SPARK", true)]
    [InlineData("pikachu", true)]  // ...and the species name still matches too
    [InlineData("zzz", false)]
    public void MatchesSearch_MatchesTheNicknameAsWellAsTheSpeciesName(string query, bool expected) =>
        Assert.Equal(expected, CollectionViewModel.MatchesSearch(EntryFor("Pikachu", "Sparky"), query));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MatchesSearch_EmptyQuery_MatchesEverything(string? query) =>
        Assert.True(CollectionViewModel.MatchesSearch(EntryFor("Pikachu", null), query));

    // ===================== Task 5: evolution filter + sort =====================

    [Fact]
    public void GetEntries_NotAtFinalStage_HasEvolutionsRemainingIsTrue()
    {
        var collection = new PokemonCollection();
        // MakeIndividual defaults to ChainLength 3, CurrentStage Stage1 (0) — two stages left.
        collection.Add(MakeIndividual(1));
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

        Assert.True(vm.GetEntries(DateTimeOffset.UtcNow).Single().HasEvolutionsRemaining);
    }

    [Fact]
    public void GetEntries_AtFinalStage_HasEvolutionsRemainingIsFalse()
    {
        var collection = new PokemonCollection();
        var individual = MakeIndividual(1);
        individual.CurrentStage = EvolutionStage.Stage3; // ChainLength 3 -> stage index 2 is final
        collection.Add(individual);
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

        Assert.False(vm.GetEntries(DateTimeOffset.UtcNow).Single().HasEvolutionsRemaining);
    }

    [Fact]
    public void ApplySort_Default_LeavesOrderUnchanged()
    {
        var entries = new List<CollectionEntry> { EntryFor("Zubat", null), EntryFor("Abra", null) };

        var sorted = CollectionViewModel.ApplySort(entries, PokedexSortOrder.Default);

        Assert.Equal(entries, sorted);
    }

    [Fact]
    public void ApplySort_Alphabetical_SortsBySpeciesNameCaseInsensitively()
    {
        var entries = new List<CollectionEntry> { EntryFor("Zubat", null), EntryFor("abra", null), EntryFor("Machop", null) };

        var sorted = CollectionViewModel.ApplySort(entries, PokedexSortOrder.Alphabetical);

        Assert.Equal(new[] { "abra", "Machop", "Zubat" }, sorted.Select(e => e.SpeciesName));
    }

    [Fact]
    public void ApplySort_CatchDate_NewestFirst()
    {
        var now = DateTimeOffset.UtcNow;
        var oldest = EntryFor("Oldest", null) with { CaughtAtUtc = now - TimeSpan.FromDays(2) };
        var newest = EntryFor("Newest", null) with { CaughtAtUtc = now };
        var middle = EntryFor("Middle", null) with { CaughtAtUtc = now - TimeSpan.FromDays(1) };

        var sorted = CollectionViewModel.ApplySort(new List<CollectionEntry> { oldest, newest, middle }, PokedexSortOrder.CatchDate);

        Assert.Equal(new[] { "Newest", "Middle", "Oldest" }, sorted.Select(e => e.SpeciesName));
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

    // ============ Type badges (Part B) and the spoiler-safe chain preview (Part C) ============

    private static readonly SpeciesEvolutionChain CaterpieLine = new(
        ChainId: 3, StarterSpeciesId: 10, CaptureRate: 255, ChainLength: 3,
        StageSpeciesIds: new[] { 10, 11, 12 },
        StageSpeciesNames: new[] { "Caterpie", "Metapod", "Butterfree" },
        StageTypes: new[] { new[] { "bug" }, new[] { "bug" }, new[] { "bug", "flying" } });

    private static CollectionViewModel MakeVm(PokemonIndividual individual, params SpeciesEvolutionChain[] chains)
    {
        var collection = new PokemonCollection();
        collection.Add(individual);
        return new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(),
            CareMode.Chill, chains, new List<PendingEgg>());
    }

    /// Types follow the CURRENT stage, not the starter species — an evolved Butterfree is
    /// Bug/Flying even though its individual is permanently SpeciesId 10.
    [Fact]
    public void GetEntries_TypesFollowTheCurrentEvolutionStage()
    {
        var individual = MakeIndividual(10, evolutionChainId: 3);
        var vm = MakeVm(individual, CaterpieLine);

        Assert.Equal(new[] { "bug" }, vm.GetEntries(DateTimeOffset.UtcNow).Single().Types);

        individual.CurrentStage = EvolutionStage.Stage3;

        Assert.Equal(new[] { "bug", "flying" }, vm.GetEntries(DateTimeOffset.UtcNow).Single().Types);
    }

    /// THE spoiler rule: an unreached stage must carry no id, no name and no types. Asserted on
    /// the MODEL rather than on the rendered card, because that is where the guarantee lives —
    /// if the identity never enters the model, no template can leak it.
    [Fact]
    public void GetEntries_EvolutionPreview_HidesTheIdentityOfUnreachedStages()
    {
        var individual = MakeIndividual(10, evolutionChainId: 3); // fresh catch => Stage1
        var stages = MakeVm(individual, CaterpieLine).GetEntries(DateTimeOffset.UtcNow).Single().EvolutionStages;

        Assert.Equal(3, stages.Count);

        Assert.True(stages[0].IsRevealed);
        Assert.Equal("Caterpie", stages[0].SpeciesName);
        Assert.True(stages[0].IsCurrent);

        foreach (var hidden in stages.Skip(1))
        {
            Assert.False(hidden.IsRevealed);
            Assert.Null(hidden.SpeciesId);
            Assert.Null(hidden.SpeciesName);
            Assert.Empty(hidden.Types);
            Assert.False(hidden.IsCurrent);
        }
    }

    /// Evolving reveals exactly one more stage — and no further.
    [Fact]
    public void GetEntries_EvolutionPreview_RevealsStagesUpToTheOneReached()
    {
        var individual = MakeIndividual(10, evolutionChainId: 3);
        individual.CurrentStage = EvolutionStage.Stage2;

        var stages = MakeVm(individual, CaterpieLine).GetEntries(DateTimeOffset.UtcNow).Single().EvolutionStages;

        Assert.True(stages[0].IsRevealed);
        Assert.Equal("Metapod", stages[1].SpeciesName);
        Assert.True(stages[1].IsCurrent);

        // The final form is still a mystery until it is actually reached.
        Assert.False(stages[2].IsRevealed);
        Assert.Null(stages[2].SpeciesName);
    }

    [Fact]
    public void GetEntries_EvolutionPreview_FullyEvolved_RevealsTheWholeLine()
    {
        var individual = MakeIndividual(10, evolutionChainId: 3);
        individual.CurrentStage = EvolutionStage.Stage3;

        var stages = MakeVm(individual, CaterpieLine).GetEntries(DateTimeOffset.UtcNow).Single().EvolutionStages;

        Assert.All(stages, stage => Assert.True(stage.IsRevealed));
        Assert.Equal("Butterfree", stages[2].SpeciesName);
        Assert.True(stages[2].IsCurrent);
    }

    /// A single-stage species has no line to preview, so the section renders nothing at all
    /// rather than a confusing one-node "chain".
    [Fact]
    public void GetEntries_EvolutionPreview_SingleStageSpecies_IsEmpty()
    {
        var emolga = new PokemonIndividual
        {
            SpeciesId = 587, EvolutionChainId = 298, ChainLength = 1,
            Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow
        };
        var chain = new SpeciesEvolutionChain(
            ChainId: 298, StarterSpeciesId: 587, CaptureRate: 200, ChainLength: 1,
            StageSpeciesIds: new[] { 587 }, StageSpeciesNames: new[] { "Emolga" },
            StageTypes: new[] { new[] { "electric", "flying" } });

        var entry = MakeVm(emolga, chain).GetEntries(DateTimeOffset.UtcNow).Single();

        Assert.Empty(entry.EvolutionStages);
        // ...but it still shows its types, and still reads as "never evolves".
        Assert.Equal(new[] { "electric", "flying" }, entry.Types);
        Assert.Equal(EvolutionProgressState.NeverEvolves, entry.EvolutionState);
    }

    /// An old catalog with no type data must degrade to "no badges", never to a crash.
    [Fact]
    public void GetEntries_ChainWithoutTypeData_YieldsNoTypesInsteadOfThrowing()
    {
        var untyped = new SpeciesEvolutionChain(
            ChainId: 3, StarterSpeciesId: 10, CaptureRate: 255, ChainLength: 3,
            StageSpeciesIds: new[] { 10, 11, 12 },
            StageSpeciesNames: new[] { "Caterpie", "Metapod", "Butterfree" });

        var entry = MakeVm(MakeIndividual(10, evolutionChainId: 3), untyped).GetEntries(DateTimeOffset.UtcNow).Single();

        Assert.Empty(entry.Types);
        Assert.Equal(3, entry.EvolutionStages.Count);
    }
}
