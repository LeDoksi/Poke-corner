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
    public async Task QueuedActivityAfterStartPublicationAndShutdownRemainderSurviveRestart()
    {
        var initial = State();
        var live = new AppState(); live.Hydrate(initial);
        var buffer = new ExpeditionActivityBuffer();
        var request = Request(initial, 360, Utc);
        var store = new JsonSaveStore(Path.Combine(root, "buffered.json"));
        var firstWrite = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = true;
        var writer = new BackgroundSaveWriter(store, action =>
        {
            if (first) { first = false; firstWrite.SetResult(action); }
            else Task.Run(action);
        });
        Task start = Task.CompletedTask;
        PokeCorner.App.Tests.Theme.WpfTestHost.Run(() =>
        {
            start = live.CommitAsync(writer, next => new ExpeditionService(Catalog.Value, next).Start(request), () => 0);
            live.RunWhenReady(() => buffer.Credit(live.RunningExpeditionId, 237));
        });
        await Task.Run(await firstWrite.Task);
        await start.WaitAsync(TimeSpan.FromSeconds(10));
        PokeCorner.App.Tests.Theme.WpfTestHost.Run(() =>
        {
            buffer.Sync(live.RunningExpeditionId);
            Assert.Equal(237, buffer.PendingActions);
        });
        Task shutdownCommit = Task.CompletedTask;
        PokeCorner.App.Tests.Theme.WpfTestHost.Run(() => shutdownCommit = buffer.CommitAsync(
            live, writer, Catalog.Value, () => 0, null, Utc.AddMinutes(1)));
        await shutdownCommit.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, buffer.PendingActions);
        Assert.Equal(237, store.Load()!.Expedition!.ActivityActions);
    }

    [Fact]
    public void FailedWriterRecoverySnapshotIncludesUncommittedActivityWithoutMutatingLiveState()
    {
        var save = State();
        var request = Request(save, 360, Utc);
        new ExpeditionService(Catalog.Value, save).Start(request);
        var live = new AppState(); live.Hydrate(save);
        var buffer = new ExpeditionActivityBuffer();
        buffer.Credit(live.RunningExpeditionId, 123);
        var candidate = live.ToSaveData(0);
        buffer.ApplyTo(candidate, Catalog.Value, Utc.AddMinutes(1));
        Assert.Equal(123, JsonSaveStore.Clone(candidate).Expedition!.ActivityActions);
        Assert.Equal(0, live.ToSaveData(0).Expedition!.ActivityActions);
        Assert.Equal(123, buffer.PendingActions);
    }

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
    public void ClaimResolvesClosedEncounterWithoutCatchingOrDiscoveringIt()
    {
        var save = State(3);
        var request = Request(save, 30, Utc);
        new ExpeditionService(Catalog.Value, save).Start(request);
        save.Expedition!.Rewards.HasEncounter = true;
        save.Expedition.Status = "ready";
        var ownedBefore = save.Individuals.Count;
        var discoveryBefore = save.DiscoveredSpeciesIds.ToHashSet();
        var shinyBefore = save.ShinySpeciesIds.ToHashSet();
        var activeBefore = save.ActiveIndividualId;
        var claim = new ExpeditionService(Catalog.Value, save).Claim(request.OperationId, Guid.NewGuid(), Utc.AddMinutes(30));
        Assert.True(claim.Success);
        Assert.Equal(ownedBefore, save.Individuals.Count);
        Assert.Equal(discoveryBefore, save.DiscoveredSpeciesIds);
        Assert.Equal(shinyBefore, save.ShinySpeciesIds);
        Assert.Equal(activeBefore, save.ActiveIndividualId);
        Assert.Equal("resolved", Assert.Single(save.Encounters).Phase);
        Assert.NotNull(claim.Encounter);
        Assert.True(claim.Encounter.IsClosed);
        Assert.Equal(save.Encounters.Single().Individual!.SpeciesId, claim.Encounter.SpeciesId);
    }

    [Fact]
    public void LaterOpenUsesFrozenClaimResultAndDoesNotSpendNewlyActivatedCharm()
    {
        var save = State(3);
        var request = Request(save, 30, Utc);
        new ExpeditionService(Catalog.Value, save).Start(request);
        save.Expedition!.Rewards.HasEncounter = true;
        save.Expedition.Status = "ready";
        var operationId = Guid.NewGuid();
        var claim = new ExpeditionService(Catalog.Value, save).Claim(request.OperationId, operationId, Utc.AddMinutes(30));
        var store = new JsonSaveStore(Path.Combine(root, "closed.json"));
        store.Save(save);
        var loaded = store.Load()!;
        var closed = Assert.Single(loaded.Encounters);
        Assert.Equal("resolved", closed.Phase);
        Assert.Equal(claim.Encounter!.SpeciesId, closed.Individual!.SpeciesId);
        Assert.Equal(claim.Encounter.IsShiny, closed.Individual.IsShiny);
        Assert.Equal(3, loaded.Individuals.Count);
        loaded.ShopCatchLuckyCharmActive = true; // activated after the claim's frozen draw
        var opened = new EncounterService(Catalog.Value).Open(loaded, closed.Id);
        Assert.Equal(closed.Individual.Id, opened.Individual.Id);
        Assert.Equal(claim.Encounter.SpeciesId, opened.Individual.SpeciesId);
        Assert.Equal(claim.Encounter.IsShiny, opened.Individual.IsShiny);
        Assert.True(loaded.ShopCatchLuckyCharmActive);
        Assert.Equal(4, loaded.Individuals.Count);
        Assert.Contains(opened.Individual.SpeciesId, loaded.DiscoveredSpeciesIds);
        Assert.Equal(opened.Individual.IsShiny, loaded.ShinySpeciesIds.Contains(opened.Individual.SpeciesId));
        var repeated = new EncounterService(Catalog.Value).Open(loaded, closed.Id);
        Assert.True(repeated.Repeated);
        Assert.Equal(4, loaded.Individuals.Count);
        var claimReplay = new ExpeditionService(Catalog.Value, loaded).Claim(request.OperationId, operationId);
        Assert.True(claimReplay.Repeated);
        Assert.False(claimReplay.Encounter!.IsClosed);
        Assert.Equal(4, loaded.Individuals.Count);
        JsonSaveStore.ValidateData(loaded);
    }

    [Fact]
    public void UnsupportedShinyAtResolutionKeepsCharmAndClosedNormalResult()
    {
        var save = State();
        var blocked = Catalog.Value.Species.First(s => Catalog.Value.CanIssueDirectly(s.Id, false)
            && !Catalog.Value.CanIssueDirectly(s.Id, true));
        save.ShopCatchLuckyCharmActive = true;
        var service = new EncounterService(Catalog.Value);
        var reserved = service.Reserve(save, "expedition", Guid.NewGuid(), blocked.Id);
        var resolved = service.Resolve(save, reserved.Id);
        Assert.True(resolved.IsClosed);
        Assert.False(resolved.IsShiny);
        Assert.True(save.ShopCatchLuckyCharmActive);
        Assert.Single(save.Individuals);
        Assert.Equal("resolved", reserved.Phase);
        JsonSaveStore.ValidateData(save);
        service.Open(save, reserved.Id);
        Assert.True(save.ShopCatchLuckyCharmActive);
        Assert.Equal(2, save.Individuals.Count);
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

    [Theory]
    [InlineData("normal", 2447, 18000)]
    [InlineData("easy", 1, 400)]
    public void FractionalTeamRateCarriesExactCoinRemainder(string difficulty, long numerator, long denominator)
    {
        var state = State(3);
        foreach (var member in state.Individuals) member.FirstTraitId = "caring-parent";
        state.Individuals[2].TotalXp = 134; // levels 1, 1, 2: exact average 4/3
        var request = Request(state, 30, Utc) with { LocationId = "lake", Difficulty = difficulty };
        var service = new ExpeditionService(Catalog.Value, state);
        service.Start(request);
        service.Claim(request.OperationId, Guid.NewGuid(), Utc.AddMinutes(30));
        Assert.Equal(numerator, state.CoinRemainder.Numerator);
        Assert.Equal(denominator, state.CoinRemainder.Denominator);
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
        SaveData? proposedCandidate = null;
        var broken = new BackgroundSaveWriter(new JsonSaveStore(Path.Combine(root, "broken.json")),
            _ => throw new IOException("synthetic failure"));
        await Assert.ThrowsAsync<IOException>(() => live.CommitAsync(broken, next =>
        {
            proposed = new ExpeditionService(Catalog.Value, next).Claim(request.OperationId, op);
            proposedCandidate = next;
        }, () => 0));
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
        Assert.Equal(proposed!.Encounter!.SpeciesId, result!.Encounter!.SpeciesId);
        Assert.Equal(proposed.Encounter.IsShiny, result.Encounter.IsShiny);
        Assert.True(result.Encounter.IsClosed);
        var proposedChild = Assert.Single(proposedCandidate!.Encounters).Individual!;
        var committedChild = Assert.Single(committed.Encounters).Individual!;
        Assert.Equal(proposedChild.Id, committedChild.Id);
        Assert.Equal(proposedChild.Nature, committedChild.Nature);
        Assert.Equal(proposedChild.FirstTraitId, committedChild.FirstTraitId);
        Assert.Equal(proposedChild.SecondTraitId, committedChild.SecondTraitId);
        Assert.Equal(!Catalog.Value.CanIssueDirectly(result.Encounter.SpeciesId, true), committed.ShopCatchLuckyCharmActive);
        Assert.True(committed.ActivityNewGuaranteed);
        var repeated = new ExpeditionService(Catalog.Value, committed).Claim(request.OperationId, op);
        Assert.True(repeated.Repeated);
        Assert.Equal(result.Encounter, repeated.Encounter);
        Assert.Equal(1, committed.Metrics["claimed-expeditions"]);
        Assert.Single(committed.Encounters);
        Assert.Equal(3, committed.Individuals.Count);
        Assert.Equal("resolved", committed.Encounters.Single().Phase);
    }
}
