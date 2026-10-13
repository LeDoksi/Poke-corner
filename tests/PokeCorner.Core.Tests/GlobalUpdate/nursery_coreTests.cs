using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Tests.GlobalUpdate;

public sealed class NurseryCoreTests : IDisposable
{
    private static readonly string Repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private readonly string root = Path.Combine(Path.GetTempPath(), "nursery-core-" + Guid.NewGuid());
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    internal CatalogPackage Catalog(int[] normal, params int[] shiny)
    {
        Directory.CreateDirectory(Path.Combine(root, "final"));
        var source = Path.Combine(Repo, "docs/design-v2");
        var data = JsonNode.Parse(File.ReadAllText(Path.Combine(source, "final/runtime-catalog.json")))!;
        var files = new List<object>();
        var gif = "GIF89a"u8.ToArray();
        foreach (var asset in new[] { "normal.gif", "shiny.gif" })
        {
            File.WriteAllBytes(Path.Combine(root, asset), gif);
            files.Add(new { target = asset, path = asset, bytes = gif.Length, sha256 = Hash(gif) });
        }
        foreach (var species in data["species"]!.AsArray())
        {
            var id = (int)species!["id"]!;
            species["normalAvailable"] = normal.Contains(id);
            species["normalAsset"] = normal.Contains(id) ? new JsonObject { ["path"] = "normal.gif", ["sha256"] = Hash(gif) } : null;
            species["shinyAvailable"] = shiny.Contains(id);
            species["shinyAsset"] = shiny.Contains(id) ? new JsonObject { ["path"] = "shiny.gif", ["sha256"] = Hash(gif) } : null;
            species["voice"] = null;
        }
        void Write(string relative, string content)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            File.WriteAllBytes(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)), bytes);
            files.Add(new { target = relative, path = relative, bytes = bytes.Length, sha256 = Hash(bytes) });
        }
        Write("final/runtime-catalog.json", data.ToJsonString());
        foreach (var name in new[] { "catalog-rules.json", "balance.json" })
            Write("final/" + name, File.ReadAllText(Path.Combine(source, "final", name)));
        Write("nursery-recipes-draft.json", File.ReadAllText(Path.Combine(source, "nursery-recipes-draft.json")));
        File.WriteAllText(Path.Combine(root, "final/runtime-package.json"),
            JsonSerializer.Serialize(new { schemaVersion = 1, catalogRevision = (string)data["dataRevision"]!, files }));
        return CatalogPackage.Load(root);
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static SaveData State(CatalogPackage catalog, params int[] speciesIds) => new()
    {
        SchemaVersion = 2, CatalogRevision = catalog.CatalogRevision, BalanceRevision = "test",
        MigrationId = SaveMigration.MigrationId, Settings = new(),
        Berries = SaveMigration.BerryIds.ToDictionary(id => id, _ => 100),
        Items = SaveMigration.ItemIds.ToDictionary(id => id, id => id == "treats" ? 0 : 2),
        BerryRemainders = SaveMigration.BerryIds.ToDictionary(id => id, _ => new RationalRemainder()),
        Individuals = speciesIds.Select(id =>
        {
            var species = catalog.Species.Single(x => x.Id == id);
            return new PokemonIndividual
            {
                SpeciesId = species.FamilyRootId, CurrentSpeciesId = id, FamilyRootId = species.FamilyRootId,
                EvolutionChainId = species.EvolutionChainId,
                ChainLength = id switch { 26 or 679 => 3, 2 or 849 or 714 => 2, _ => 1 },
                ReachedSpeciesIds = id switch { 26 => [172, 25, 26], 2 => [1, 2], 849 => [848, 849], _ => [id] },
                CurrentStage = id switch { 26 => EvolutionStage.Stage3, 2 or 849 => EvolutionStage.Stage2, _ => EvolutionStage.Stage1 },
                Nature = Nature.Bold, IsShiny = false, CaughtAtUtc = Now, FirstTraitId = "gatherer"
            };
        }).ToList()
    };
    private static NurseryRequest Request(SaveData state, int? chosen = null, Guid? id = null) =>
        new(state.Individuals.Take(2).Select(x => x.Id).ToArray(), chosen, id ?? Guid.NewGuid(), Now);

    [Fact]
    public void EvolvedParentsRootReadyLocksAndDoubleHatch()
    {
        var catalog = Catalog([26, 172], 172);
        var state = State(catalog, 26, 26);
        var service = new NurseryService(catalog, state);
        var request = Request(state);
        Assert.Equal(172, Assert.Single(service.Preview(request.ParentIds).Outcomes).SpeciesId);
        var launch = service.Start(request);
        Assert.True(launch.Success);
        Assert.Null(launch.Child);
        Assert.Equal(172, state.Nest!.Child.SpeciesId);
        Assert.Equal(3, state.Nest.Child.ChainLength);
        Assert.All(request.ParentIds, id => Assert.Contains(id, ExpeditionService.BusyIndividualIds(state)));
        Assert.True(service.RefreshReady(Now.AddHours(2)));
        Assert.False(service.RefreshReady(Now.AddHours(-1)));
        Assert.Equal("ready", state.Nest.Status);
        var op = Guid.NewGuid();
        Assert.True(service.Hatch(request.OperationId, op, Now.AddHours(2)).Success);
        Assert.True(service.Hatch(request.OperationId, op).Repeated);
        Assert.False(service.Hatch(request.OperationId, Guid.NewGuid()).Success);
        Assert.Equal(3, state.Individuals.Count);
        Assert.Empty(ExpeditionService.BusyIndividualIds(state));
    }

    [Fact]
    public void BusyAndInvalidSelectionDoNotSpend()
    {
        var catalog = Catalog([2, 849, 451, 453, 803]);
        var state = State(catalog, 2, 849);
        var service = new NurseryService(catalog, state);
        var request = Request(state, 451);
        Assert.Equal(3, service.Preview(request.ParentIds).Outcomes.Count);
        Assert.Throws<InvalidOperationException>(() => service.Start(request with { SelectedSpeciesId = 999 }));
        state.Expedition = new ExpeditionState { Id = Guid.NewGuid(), ParticipantIds = [state.Individuals[0].Id],
            Status = "ready", StartedAtUtc = Now, ReadyAtUtc = Now, DurationMinutes = 30 };
        Assert.Throws<InvalidOperationException>(() => service.Start(request));
        Assert.Null(state.Nest);
        Assert.Equal(2, state.Items["offspring-charm"]);
        Assert.All(state.Berries.Values, n => Assert.Equal(100, n));
    }

    [Fact]
    public void SelectionSpendsCanonicalPriceAndChoiceItemWithChosenChild()
    {
        var catalog = Catalog([2, 849, 451, 453, 803], 451);
        var state = State(catalog, 2, 849);
        state.ShopCatchLuckyCharmActive = true;
        var service = new NurseryService(catalog, state);
        var request = Request(state, 803);
        var preview = service.Preview(request.ParentIds, request.SelectedSpeciesId, Now);
        Assert.True(preview.SelectionUseful);
        Assert.Equal(14, preview.Berries.Values.Sum());
        Assert.Equal(1, preview.OutcomeChances[803]);
        Assert.Equal(0, preview.OutcomeChances[451]);
        Assert.Equal(0, preview.ShinyChances[803]);
        Assert.True(preview.ShinyChances[451] > 0);
        Assert.True(service.Start(request).Success);
        Assert.Equal(803, state.Nest!.Child.SpeciesId);
        Assert.False(state.Nest.ShinySupported);
        Assert.True(state.ShopCatchLuckyCharmActive);
        Assert.Equal(1, state.Items["offspring-charm"]);
        foreach (var (id, count) in preview.Berries) Assert.Equal(100 - count, state.Berries[id]);
        Assert.True(service.Start(request).Repeated);
        Assert.Equal(1, state.Items["offspring-charm"]);
        var sameRequest = request with { ParentIds = request.ParentIds.Reverse().ToArray() };
        Assert.True(service.Start(sameRequest).Repeated);
        Assert.Throws<InvalidOperationException>(() => service.Start(request with { SelectedSpeciesId = 451 }));
    }

    [Fact]
    public void SingleAvailableOutcomeRejectsChoiceWithoutSpendingCharm()
    {
        var catalog = Catalog([2, 849, 451]);
        var state = State(catalog, 2, 849);
        var service = new NurseryService(catalog, state);
        var preview = service.Preview(Request(state).ParentIds);
        Assert.False(preview.SelectionUseful);
        Assert.Single(preview.Outcomes);
        Assert.Equal(10, preview.Berries.Values.Sum());
        Assert.Throws<InvalidOperationException>(() => service.Start(Request(state, 451)));
        Assert.Equal(2, state.Items["offspring-charm"]);
        Assert.Null(state.Nest);
    }

    [Fact]
    public void RandomPreviewRetainsCanonicalWeightsAndCaringParentsShortenTime()
    {
        var catalog = Catalog([2, 849, 451, 453, 803]);
        var state = State(catalog, 2, 849);
        foreach (var parent in state.Individuals) parent.FirstTraitId = "caring-parent";
        var service = new NurseryService(catalog, state);
        var request = Request(state);
        var preview = service.Preview(request.ParentIds, now: Now);
        Assert.Equal(1, preview.OutcomeChances.Values.Sum(), 10);
        Assert.Equal(8d / 20, preview.OutcomeChances[451], 10);
        Assert.Equal(8d / 20, preview.OutcomeChances[453], 10);
        Assert.Equal(4d / 20, preview.OutcomeChances[803], 10);
        Assert.Equal(Now.AddHours(6).AddMinutes(-27), preview.ReadyAtUtc);
        Assert.All(preview.TraitsBySpecies.Values, pool => Assert.Equal(new[] { "caring-parent" }, pool));
    }

    [Fact]
    public async Task FailedCommitsAndRestartRetainOutcomeWithoutDoubleSpendOrGrant()
    {
        var catalog = Catalog([26, 172], 172);
        var original = State(catalog, 26, 26);
        var live = new AppState(); live.Hydrate(original);
        var request = Request(original);
        PokemonIndividual? proposed = null;
        var broken = new BackgroundSaveWriter(new JsonSaveStore(Path.Combine(root, "failed-start.json")),
            _ => throw new IOException("synthetic failure"));
        await Assert.ThrowsAsync<IOException>(() => live.CommitAsync(broken,
            next => { new NurseryService(catalog, next).Start(request); proposed = JsonSaveStore.Clone(next).Nest!.Child; }, () => 0));
        Assert.Null(live.ToSaveData(0).Nest);
        var store = new JsonSaveStore(Path.Combine(root, "state.json"));
        store.Save(original);
        NurseryAction? launched = null;
        await live.CommitAsync(new BackgroundSaveWriter(store),
            next => launched = new NurseryService(catalog, next).Start(request), () => 0);
        var savedStart = store.Load()!;
        Assert.Null(launched!.Child);
        Assert.Equal(proposed!.Id, savedStart.Nest!.Child.Id);
        Assert.Equal(proposed.FirstTraitId, savedStart.Nest.Child.FirstTraitId);
        Assert.Equal(proposed.IsShiny, savedStart.Nest.Child.IsShiny);
        Assert.Equal(90, savedStart.Berries["oran"]);
        var liveRestart = new AppState(); liveRestart.Hydrate(savedStart);
        var hatchId = Guid.NewGuid();
        NurseryAction? proposedHatch = null;
        var brokenHatch = new BackgroundSaveWriter(new JsonSaveStore(Path.Combine(root, "failed-hatch.json")),
            _ => throw new IOException("synthetic failure"));
        await Assert.ThrowsAsync<IOException>(() => liveRestart.CommitAsync(brokenHatch,
            next => proposedHatch = new NurseryService(catalog, next).Hatch(request.OperationId, hatchId, Now.AddHours(2)), () => 0));
        Assert.Equal(2, liveRestart.ToSaveData(0).Individuals.Count);
        Assert.Equal("running", liveRestart.ToSaveData(0).Nest!.Status);
        NurseryAction? hatched = null;
        await liveRestart.CommitAsync(new BackgroundSaveWriter(store),
            next => hatched = new NurseryService(catalog, next).Hatch(request.OperationId, hatchId, Now.AddHours(2)), () => 0);
        var committed = store.Load()!;
        Assert.Equal(proposedHatch!.Child!.Id, hatched!.Child!.Id);
        Assert.Equal(3, committed.Individuals.Count);
        Assert.Equal(90, committed.Berries["oran"]);
        Assert.True(new NurseryService(catalog, committed).Hatch(request.OperationId, hatchId).Repeated);
        Assert.Equal(3, committed.Individuals.Count);
    }

    [Fact]
    public void FastGrowerWithoutEvolutionFallsBackToSharedValidPool()
    {
        var catalog = Catalog([870]);
        var state = State(catalog, 870, 870);
        foreach (var parent in state.Individuals) parent.FirstTraitId = "fast-grower";
        var service = new NurseryService(catalog, state);
        var request = Request(state);
        var preview = service.Preview(request.ParentIds);
        Assert.DoesNotContain("fast-grower", preview.TraitIds);
        Assert.NotEmpty(preview.TraitIds);
        service.Start(request);
        Assert.Contains(state.Nest!.Child.FirstTraitId, preview.TraitIds);
        Assert.Equal(1, state.Nest.Child.ChainLength);
        Assert.All(state.Individuals, parent => Assert.Equal("fast-grower", parent.FirstTraitId));
    }

    [Fact]
    public void RootWithBlockedNormalNextStageCannotInheritFastGrower()
    {
        // Honedge is a real family root; the next normal stage #680 has no asset.
        var catalog = Catalog([679]);
        var state = State(catalog, 679, 679);
        foreach (var parent in state.Individuals) parent.FirstTraitId = "fast-grower";
        var service = new NurseryService(catalog, state);
        var request = Request(state);
        Assert.DoesNotContain("fast-grower", service.Preview(request.ParentIds).TraitsBySpecies[679]);
        service.Start(request);
        Assert.NotEqual("fast-grower", state.Nest!.Child.FirstTraitId);
    }

    [Fact]
    public void ShinyRootWithBlockedShinyNextStageCannotInheritFastGrower()
    {
        // Noibat #714 is a real root with shiny art; #715 has normal but no shiny art.
        var catalog = Catalog([714, 715], 714);
        for (var i = 1; i <= 50; i++)
        {
            var state = State(catalog, 714, 714);
            foreach (var parent in state.Individuals) parent.FirstTraitId = "fast-grower";
            state.NurseryLuckBonus = ShinyRules.NurseryChanceCap - ShinyRules.BaseChance;
            state.ShopCatchLuckyCharmActive = true;
            var service = new NurseryService(catalog, state);
            var preview = service.Preview(Request(state).ParentIds);
            Assert.Contains("fast-grower", preview.TraitsBySpecies[714]);
            Assert.DoesNotContain("fast-grower", preview.ShinyTraitsBySpecies[714]);
            var id = new Guid(i, 0, 0, new byte[8]);
            service.Start(Request(state, id: id));
            if (!state.Nest!.Child.IsShiny) continue;
            Assert.NotEqual("fast-grower", state.Nest.Child.FirstTraitId);
            return;
        }
        Assert.Fail("No shiny child was rolled in 50 deterministic launch IDs.");
    }

    [Fact]
    public void HatchReplayReturnsFrozenChildAfterLegalEvolutionAndRelease()
    {
        var catalog = Catalog([714, 715]);
        var state = State(catalog, 714, 714);
        var service = new NurseryService(catalog, state);
        var request = Request(state);
        service.Start(request);
        var hatchId = Guid.NewGuid();
        var result = service.Hatch(request.OperationId, hatchId, Now.AddHours(2));
        var owned = state.Individuals.Single(x => x.Id == result.Child!.Id);
        owned.CurrentSpeciesId = 715;
        owned.ReachedSpeciesIds.Add(715);
        owned.CurrentStage = EvolutionStage.Stage2;
        Assert.Equal(714, service.Hatch(request.OperationId, hatchId).Child!.CurrentSpeciesId);
        state.Individuals.Remove(owned);
        Assert.Equal(714, service.Hatch(request.OperationId, hatchId).Child!.CurrentSpeciesId);
        Assert.Equal(2, state.Individuals.Count);
        JsonSaveStore.ValidateData(state);
        var reloaded = JsonSaveStore.Clone(state);
        var nextRequest = Request(reloaded, id: Guid.NewGuid());
        new NurseryService(catalog, reloaded).Start(nextRequest);
        var replay = new NurseryService(catalog, reloaded).Hatch(request.OperationId, hatchId);
        Assert.True(replay.Repeated);
        Assert.Equal(714, replay.Child!.CurrentSpeciesId);
        Assert.Equal(2, reloaded.Individuals.Count);
        JsonSaveStore.ValidateData(reloaded);
    }

    [Theory]
    [InlineData("running")]
    [InlineData("ready")]
    public void BusyNestParentCannotEvolveThroughAppState(string phase)
    {
        var catalog = Catalog([679]);
        var state = State(catalog, 679, 679);
        state.Individuals[0].AccumulatedCarePoints = int.MaxValue;
        var request = Request(state);
        new NurseryService(catalog, state).Start(request);
        state.Nest!.Status = phase;
        var live = new AppState(); live.Hydrate(state);
        var parent = live.Collection.Individuals[0];
        Assert.False(live.IsReadyToEvolve(parent));
        Assert.False(live.TryEvolve(parent));
        Assert.Equal(EvolutionStage.Stage1, parent.CurrentStage);
        JsonSaveStore.ValidateData(live.ToSaveData(0));
    }

    [Theory]
    [InlineData("running")]
    [InlineData("ready")]
    public void BusyExpeditionParentCannotEvolveThroughAppState(string phase)
    {
        var catalog = Catalog([679]);
        var state = State(catalog, 679, 679);
        state.Individuals[0].AccumulatedCarePoints = int.MaxValue;
        state.Expedition = new ExpeditionState { Id = Guid.NewGuid(), ParticipantIds = [state.Individuals[0].Id],
            Status = phase, StartedAtUtc = Now, ReadyAtUtc = Now.AddMinutes(30), DurationMinutes = 30,
            LocationId = "grove", Difficulty = "easy", Rewards = new ExpeditionRewards { PrimaryBerryId = "oran" },
            Conditions = JsonSerializer.SerializeToElement(new { }), Outcomes = JsonSerializer.SerializeToElement(new { }) };
        var live = new AppState(); live.Hydrate(state);
        var parent = live.Collection.Individuals[0];
        Assert.False(live.IsReadyToEvolve(parent));
        Assert.False(live.TryEvolve(parent));
        Assert.Equal(EvolutionStage.Stage1, parent.CurrentStage);
        JsonSaveStore.ValidateData(live.ToSaveData(0));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LuckAndCharmFollowActualShinyEligibility(bool supported)
    {
        var catalog = supported ? Catalog([26, 172], 172) : Catalog([870]);
        var state = supported ? State(catalog, 26, 26) : State(catalog, 870, 870);
        state.ShopCatchLuckyCharmActive = true;
        state.NurseryLuckBonus = .02;
        var service = new NurseryService(catalog, state);
        var request = Request(state);
        service.Start(request);
        Assert.Equal(!supported, state.ShopCatchLuckyCharmActive);
        Assert.Equal(.02, state.NurseryLuckBonus);
        var child = state.Nest!.Child;
        Assert.Equal(supported, state.Nest.ShinySupported);
        service.Hatch(request.OperationId, Guid.NewGuid(), Now.AddHours(8));
        Assert.Equal(ShinyRules.NextNurseryLuck(.02, child.IsShiny, supported), state.NurseryLuckBonus);
    }
}
