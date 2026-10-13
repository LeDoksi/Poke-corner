using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Tests.GlobalUpdate;

public sealed class ExpeditionCoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ExpeditionTest_" + Guid.NewGuid().ToString("N"));
    private static readonly Lazy<CatalogPackage> Catalog = new(() => CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data")));
    private static readonly DateTimeOffset Utc = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    public ExpeditionCoreTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);

    private static SaveData State(int count = 1)
    {
        var species = Catalog.Value.Species.First(s => Catalog.Value.CanIssueDirectly(s.Id, false));
        return new SaveData
        {
            SchemaVersion = 2, CatalogRevision = Catalog.Value.CatalogRevision, BalanceRevision = "test", MigrationId = SaveMigration.MigrationId,
            Settings = new(), Berries = SaveMigration.BerryIds.ToDictionary(id => id, _ => 0),
            Items = SaveMigration.ItemIds.ToDictionary(id => id, _ => 0),
            BerryRemainders = SaveMigration.BerryIds.ToDictionary(id => id, _ => new RationalRemainder()),
            Individuals = Enumerable.Range(0, count).Select(_ => new PokemonIndividual
            {
                SpeciesId = species.Id, CurrentSpeciesId = species.Id, FamilyRootId = species.Id,
                EvolutionChainId = species.EvolutionChainId, ChainLength = 3, ReachedSpeciesIds = [species.Id],
                Nature = Nature.Bold, IsShiny = false, CaughtAtUtc = Utc, FirstTraitId = "gatherer"
            }).ToList()
        };
    }
    private static ExpeditionRequest Request(SaveData state, int minutes, DateTimeOffset now) =>
        new("grove", "normal", minutes, state.Individuals.Select(x => x.Id).ToArray(), Guid.NewGuid(), now);

    [Fact]
    public void OfflineUtcAccelerationReadyAndBusySurviveRestart()
    {
        var state = State();
        var request = Request(state, 360, Utc.ToOffset(TimeSpan.FromHours(3)));
        var service = new ExpeditionService(Catalog.Value, state);
        service.Start(request);
        Assert.Equal(TimeSpan.Zero, state.Expedition!.StartedAtUtc.Offset);
        var original = state.Expedition.ReadyAtUtc;
        service.ApplyActivityActions(5_000, Utc.AddMinutes(1));
        Assert.Equal(original.AddMinutes(-36), state.Expedition.ReadyAtUtc);
        var saved = JsonSaveStore.Clone(state);
        var restored = new ExpeditionService(Catalog.Value, saved);
        Assert.Equal(5_000, saved.Expedition!.ActivityActions);
        Assert.True(restored.RefreshReady(original.AddMinutes(-1)));
        Assert.False(restored.RefreshReady(Utc.AddHours(-10)));
        Assert.Equal("ready", saved.Expedition.Status);
        Assert.Contains(state.Individuals[0].Id, ExpeditionService.BusyIndividualIds(saved));
        Assert.False(restored.Cancel(saved.Expedition.Id, Utc.AddHours(-10)).Success);
    }

    [Fact]
    public void TwelveHalfHoursEqualSixHoursIncludingRemainder()
    {
        var shortTrips = State();
        var longTrip = JsonSaveStore.Clone(shortTrips);
        for (var i = 0; i < 12; i++)
        {
            var now = Utc.AddMinutes(i * 30);
            var service = new ExpeditionService(Catalog.Value, shortTrips);
            var request = Request(shortTrips, 30, now);
            service.Start(request);
            Assert.True(service.Claim(request.OperationId, Guid.NewGuid(), now.AddMinutes(30)).Success);
        }
        var six = new ExpeditionService(Catalog.Value, longTrip);
        var longRequest = Request(longTrip, 360, Utc);
        six.Start(longRequest);
        six.Claim(longRequest.OperationId, Guid.NewGuid(), Utc.AddHours(6));
        Assert.Equal(12, shortTrips.Berries["rawst"]);
        Assert.Equal(longTrip.Berries["rawst"], shortTrips.Berries["rawst"]);
        Assert.Equal(longTrip.BerryRemainders["rawst"].Numerator, shortTrips.BerryRemainders["rawst"].Numerator);
        Assert.Equal(longTrip.BerryRemainders["rawst"].Denominator, shortTrips.BerryRemainders["rawst"].Denominator);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void PartialTeamsHaveNoExtrasOrEncounter(int count)
    {
        var state = State(count);
        var service = new ExpeditionService(Catalog.Value, state);
        var request = Request(state, 360, Utc);
        Assert.Equal(0, service.Preview(request).EncounterChance);
        service.Start(request);
        Assert.Empty(state.Expedition!.Rewards.ExtraBerries);
        Assert.Empty(state.Expedition.Rewards.ExtraItems);
        Assert.Equal(0, state.Expedition.Rewards.ExtraCoins);
        Assert.False(state.Expedition.Rewards.HasEncounter);
    }

    [Fact]
    public void SixHourLevelTenExampleAndCancelBoundary()
    {
        var state = State(3);
        foreach (var member in state.Individuals) { member.TotalXp = 1200; member.FirstTraitId = "caring-parent"; }
        var service = new ExpeditionService(Catalog.Value, state);
        var request = Request(state, 360, Utc) with { LocationId = "lake" };
        var preview = service.Preview(request);
        Assert.Equal(12, preview.PrimaryBerries);
        Assert.Equal(49, preview.Coins);
        Assert.Equal(600, preview.XpPerParticipant);
        service.Start(request);
        Assert.Throws<InvalidOperationException>(() => service.Start(Request(state, 30, Utc)));
        Assert.False(service.Cancel(request.OperationId, preview.ReadyAtUtc).Success);
        Assert.Equal("running", state.Expedition!.Status);
        var claim = service.Claim(request.OperationId, Guid.NewGuid(), preview.ReadyAtUtc);
        Assert.True(claim.Success);
        Assert.Empty(ExpeditionService.BusyIndividualIds(state));
        Assert.Equal(49 + state.Expedition.Rewards.ExtraCoins, state.ShopCoins);
    }

    [Fact]
    public void EarlyCancellationReleasesParticipantsWithoutAwardsAndReplays()
    {
        var state = State();
        var service = new ExpeditionService(Catalog.Value, state);
        var request = Request(state, 30, Utc);
        service.Start(request);
        Assert.True(service.Cancel(request.OperationId, Utc.AddMinutes(29)).Success);
        Assert.Null(state.Expedition);
        Assert.Empty(ExpeditionService.BusyIndividualIds(state));
        Assert.Empty(state.Berries.Where(x => x.Value != 0));
        Assert.Equal(0, state.Individuals[0].TotalXp);
        Assert.True(service.Cancel(request.OperationId, Utc).Repeated);
    }

    [Fact]
    public async Task ClaimFailureKeepsReadyAndRetryGrantsOnce()
    {
        var state = State(3);
        state.ShopCatchLuckyCharmActive = true;
        state.ActivityNewGuaranteed = true;
        var request = Request(state, 30, Utc);
        new ExpeditionService(Catalog.Value, state).Start(request);
        state.Expedition!.Rewards.HasEncounter = true;
        state.Expedition.Status = "ready";
        var live = new AppState();
        live.Hydrate(state);
        var op = Guid.NewGuid();
        ExpeditionAction? proposed = null;
        var broken = new BackgroundSaveWriter(new JsonSaveStore(Path.Combine(root, "broken.json")),
            _ => throw new IOException("synthetic failure"));
        await Assert.ThrowsAsync<IOException>(() => live.CommitAsync(broken, next =>
            proposed = new ExpeditionService(Catalog.Value, next).Claim(request.OperationId, op), () => 0));
        Assert.Equal("ready", live.ToSaveData(0).Expedition!.Status);
        Assert.Empty(live.Encounters);
        Assert.All(live.Collection.Individuals, p => Assert.Equal(0, p.TotalXp));
        var store = new JsonSaveStore(Path.Combine(root, "state.json"));
        store.Save(state);
        ExpeditionAction? result = null;
        await live.CommitAsync(new BackgroundSaveWriter(store), next =>
            result = new ExpeditionService(Catalog.Value, next).Claim(request.OperationId, op), () => 0);
        var committed = store.Load()!;
        Assert.Equal("claimed", committed.Expedition!.Status);
        Assert.Equal(1, committed.Metrics["claimed-expeditions"]);
        Assert.Equal(1, committed.Metrics["visited-regions"]);
        Assert.Empty(ExpeditionService.BusyIndividualIds(committed));
        Assert.True(committed.ShopCoins > 0);
        Assert.All(committed.Individuals.Take(3), p => Assert.True(p.TotalXp > 0));
        Assert.Equal(proposed!.Encounter!.Individual.SpeciesId, result!.Encounter!.Individual.SpeciesId);
        Assert.Equal(proposed.Encounter.Individual.IsShiny, result.Encounter.Individual.IsShiny);
        Assert.Equal(!Catalog.Value.CanIssueDirectly(result.Encounter.Individual.SpeciesId, true), committed.ShopCatchLuckyCharmActive);
        Assert.True(committed.ActivityNewGuaranteed);
        Assert.True(new ExpeditionService(Catalog.Value, committed).Claim(request.OperationId, op).Repeated);
        Assert.Equal(1, committed.Metrics["claimed-expeditions"]);
        Assert.Single(committed.Encounters);
        Assert.Equal(4, committed.Individuals.Count);
    }
}
