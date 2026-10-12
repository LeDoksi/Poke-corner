using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;
using PokeCorner.Core.Activity;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using System.Windows;
using System.Windows.Controls;

namespace PokeCorner.App.Tests.GlobalUpdate;

public sealed class EncountersTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "EncounterTest_" + Guid.NewGuid());
    private static string DataRoot => Path.Combine(AppContext.BaseDirectory, "Data");
    private static readonly Lazy<CatalogPackage> Catalog = new(() => CatalogPackage.Load(DataRoot));
    private static readonly Lazy<LegacyLookup> Lookup = new(() => LegacyLookup.Load(DataRoot));

    public EncountersTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);

    private static SaveData State() => new()
    {
        SchemaVersion = 2, CatalogRevision = Catalog.Value.CatalogRevision, BalanceRevision = "test", MigrationId = SaveMigration.MigrationId,
        Individuals = [], Settings = new(), Berries = SaveMigration.BerryIds.ToDictionary(id => id, _ => 0),
        Items = SaveMigration.ItemIds.ToDictionary(id => id, _ => 0),
        BerryRemainders = SaveMigration.BerryIds.ToDictionary(id => id, _ => new RationalRemainder())
    };

    [Fact]
    public async Task ActivityOpenSurvivesRestartAndRepeatedGrant()
    {
        var service = new EncounterService(Catalog.Value);
        var state = State();
        var id = Guid.NewGuid();
        service.Reserve(state, "activity", id);
        var store = new JsonSaveStore(Path.Combine(root, "state.json"));
        store.Save(state);
        var live = new AppState();
        live.Hydrate(store.Load()!);
        var writer = new BackgroundSaveWriter(store);
        EncounterOpenResult? result = null;
        await live.CommitAsync(writer, next => result = service.Open(next, id), () => 1);
        Assert.NotNull(result);
        Assert.Single(live.Collection.Individuals);
        Assert.Equal(0, live.ToSaveData(0).PendingCatchCount);
        var reopened = store.Load()!;
        var again = service.Open(reopened, id);
        Assert.True(again.Repeated);
        Assert.Equal(result.Individual.Id, again.Individual.Id);
        Assert.Single(reopened.Individuals);
    }

    [Fact]
    public async Task FailedWriteDoesNotPublishOrChangeNextOutcome()
    {
        var service = new EncounterService(Catalog.Value);
        var state = State();
        var id = Guid.NewGuid();
        service.Reserve(state, "activity", id);
        var live = new AppState();
        live.Hydrate(state);
        var failed = new BackgroundSaveWriter(new JsonSaveStore(Path.Combine(root, "missing", "state.json")),
            _ => throw new IOException("synthetic write failure"));
        EncounterOpenResult? first = null;
        await Assert.ThrowsAsync<IOException>(() => live.CommitAsync(failed, next => first = service.Open(next, id), () => 1));
        Assert.Empty(live.Collection.Individuals);
        Assert.Equal("sealed", live.Encounters.Single().Phase);
        var retry = new EncounterService(Catalog.Value).Open(live.ToSaveData(1), id);
        Assert.Equal(first!.Individual.SpeciesId, retry.Individual.SpeciesId);
        Assert.Equal(first.Individual.Id, retry.Individual.Id);
        Assert.Equal(first.Individual.FirstTraitId, retry.Individual.FirstTraitId);
    }

    [Fact]
    public void OtherSourcesRefreshUnknownPoolWithoutChangingActivityGuarantee()
    {
        var service = new EncounterService(Catalog.Value);
        var state = State();
        state.ActivityNewGuaranteed = true;
        var rootSpecies = Catalog.Value.Species.First(s => Catalog.Value.CanIssueDirectly(s.Id, false));
        var expedition = service.Reserve(state, "expedition", Guid.NewGuid(), rootSpecies.Id);
        service.Open(state, expedition.Id);
        Assert.True(state.ActivityNewGuaranteed);
        Assert.Contains(rootSpecies.Id, state.DiscoveredSpeciesIds);
        var activity = service.Reserve(state, "activity", Guid.NewGuid());
        Assert.Null(activity.Individual);
        Assert.Equal("sealed", activity.Phase);
        Assert.True(state.ActivityNewGuaranteed);
    }

    [Fact]
    public void NoveltyChanceAndGuaranteeUseDiscoveredHistory()
    {
        Assert.Equal(22_500, ActivityAccumulator.CatchCheckIntervalUnits);
        var known = new SpeciesEvolutionChain(1, 1, 200, 1, [1], ["known"]);
        var unknown = new SpeciesEvolutionChain(2, 2, 200, 1, [2], ["unknown"]);
        CatchResult Roll(double value, bool guarantee) => new CatchRoller(new FixedRandom(value)).RollActivity(
            [known, unknown], new HashSet<int> { 1 }, guarantee, [60, 30, 9, 1], .8, _ => false, _ => 1);
        Assert.Equal(2, Roll(.79, false).SpeciesId);
        Assert.Equal(1, Roll(.81, false).SpeciesId);
        Assert.Equal(2, Roll(.99, true).SpeciesId);
        Assert.Equal(1, new CatchRoller(new FixedRandom(.01)).RollActivity([known], new HashSet<int> { 1 }, true,
            [60, 30, 9, 1], .8, _ => false, _ => 1).SpeciesId);
    }

    [Fact]
    public void GiftUsesItsPreviouslyReservedIndividualWithoutRevealingOrGrantingTwice()
    {
        var service = new EncounterService(Catalog.Value);
        var chain = Lookup.Value.Chains.First(c => Catalog.Value.CanIssueDirectly(c.StarterSpeciesId, false));
        var gift = new PokemonIndividual
        {
            SpeciesId = chain.StarterSpeciesId, EvolutionChainId = chain.ChainId, ChainLength = chain.ChainLength,
            Nature = Nature.Bold, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow, AcquisitionSource = "gift"
        };
        SaveMigration.InitializeIndividual(gift, Lookup.Value, Catalog.Value);
        var state = State();
        state.Gift.Gifts = [new ReservedGift { Individual = gift }];
        var id = Guid.NewGuid();
        var sealedBall = service.ReserveGift(state, id, gift.Id);
        Assert.Null(sealedBall.Individual);
        Assert.Null(sealedBall.KnownSpeciesId);
        var opened = service.Open(state, id);
        Assert.Equal(gift.Id, opened.Individual.Id);
        Assert.Equal(Nature.Bold, opened.Individual.Nature);
        Assert.Equal("opened", state.Gift.Gifts.Single().Status);
        Assert.True(service.Open(state, id).Repeated);
        Assert.Single(state.Individuals);
    }

    [Fact]
    public void UnsupportedShinyKeepsActivatedCharm()
    {
        var blocked = Lookup.Value.Chains.First(c => Catalog.Value.CanIssueDirectly(c.StarterSpeciesId, false)
            && !Catalog.Value.CanIssueDirectly(c.StarterSpeciesId, true));
        var state = State();
        state.ShopCatchLuckyCharmActive = true;
        var service = new EncounterService(Catalog.Value);
        var reserved = service.Reserve(state, "expedition", Guid.NewGuid(), blocked.StarterSpeciesId);
        var opened = service.Open(state, reserved.Id);
        Assert.False(opened.Individual.IsShiny);
        Assert.True(state.ShopCatchLuckyCharmActive);
    }

    [Fact]
    public void EveryCanonicalDirectRootHasAChainForEncounterIssuance()
    {
        var expected = Catalog.Value.Species.Where(s => Catalog.Value.CanIssueDirectly(s.Id, false)).Select(s => s.Id).ToHashSet();
        var actual = new EncounterService(Catalog.Value).DirectRootIds.ToHashSet();
        Assert.Equal(expected.Order(), actual.Order());
    }

    [Fact]
    public void CanonicalRootMissingFromLegacyLookupCanStillBeGranted()
    {
        var service = new EncounterService(Catalog.Value);
        var legacyIds = Lookup.Value.Chains.Select(c => c.StarterSpeciesId).ToHashSet();
        var missing = service.DirectRootIds.First(id => !legacyIds.Contains(id));
        var state = State();
        var encounter = service.Reserve(state, "expedition", Guid.NewGuid(), missing);
        var opened = service.Open(state, encounter.Id);
        Assert.Equal(missing, opened.Individual.SpeciesId);
        Assert.Equal(missing, opened.Individual.FamilyRootId);
        JsonSaveStore.ValidateData(state);
    }

    [Fact]
    public void EarnedActivityBallsAreNotDiscardedAfterThree()
    {
        var service = new EncounterService(Catalog.Value);
        var state = State();
        for (var i = 0; i < 5; i++) service.Reserve(state, "activity", Guid.NewGuid());
        Assert.Equal(5, state.PendingCatchCount);
        Assert.Equal(5, state.Encounters.Count(e => e.Phase == "sealed"));
        JsonSaveStore.ValidateData(state);
    }

    [Fact]
    public void ClosedActivityBallShowsNoSpeciesOrProperties()
    {
        WpfTestHost.Run(() =>
        {
            var state = new AppState();
            state.Hydrate(State());
            state.Encounters.Add(new EncounterState { Id = Guid.NewGuid(), Source = "activity", ReservedAtUtc = DateTimeOffset.UtcNow });
            var view = new CatchView(state, () => null, _ => Task.FromResult<byte[]?>(null), () => 1, () => { }, _ => "SECRET");
            Assert.Equal(Visibility.Collapsed, ((TextBlock)view.FindName("RevealNameText")!).Visibility);
            Assert.Equal(Visibility.Collapsed, ((TextBlock)view.FindName("RevealRarityText")!).Visibility);
            var status = ((TextBlock)view.FindName("StatusText")!).Text;
            Assert.DoesNotContain("SECRET", status);
            Assert.Contains("покебол", status, StringComparison.OrdinalIgnoreCase);
        });
    }

    private sealed class FixedRandom(double value) : Random
    {
        public override double NextDouble() => value;
        public override int Next(int maxValue) => 0;
    }
}
