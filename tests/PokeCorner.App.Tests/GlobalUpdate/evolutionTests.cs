using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PokeCorner.App.Tests.GlobalUpdate;

public sealed class EvolutionTests
{
    private static readonly Lazy<CatalogPackage> Catalog = new(() => CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data")));

    private static ConfirmDialog? VisibleRetrainingConfirmation() => Application.Current.Windows
        .OfType<ConfirmDialog>()
        .FirstOrDefault(dialog => dialog.IsVisible &&
            ((TextBlock)dialog.FindName("TitleText")).Text == "Подтвердить переобучение?");

    [Fact]
    public void RetrainingDialogObserverIgnoresHiddenUnrelatedDialog() => WpfTestHost.Run(() =>
    {
        var hidden = new ConfirmDialog("Другой диалог", "Не подтверждать", "OK");
        try
        {
            Assert.Contains(hidden, Application.Current.Windows.OfType<ConfirmDialog>());
            Assert.False(hidden.IsVisible);
            Assert.NotSame(hidden, VisibleRetrainingConfirmation());
        }
        finally { hidden.Close(); }
    });

    private static SaveData MigrationWithEvolvableGift()
    {
        var starter = new PokemonIndividual
        {
            SpeciesId = 1, EvolutionChainId = 1, ChainLength = 3, Nature = Nature.Hardy,
            IsShiny = false, CaughtAtUtc = DateTimeOffset.Parse("2020-01-02T03:04:05Z")
        };
        var v1 = new SaveData { Individuals = [starter], ActiveIndividualId = starter.Id, Settings = new() };
        var migrated = SaveMigration.Migrate(v1, LegacyLookup.Load(Catalog.Value.DataRoot), Catalog.Value);
        using var rules = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Catalog.Value.DataRoot, "final", "catalog-rules.json")));
        var evolving = rules.RootElement.GetProperty("evolutionEdges").EnumerateArray()
            .Select(edge => edge.GetProperty("from").GetInt32()).ToHashSet();
        Assert.Contains(migrated.Gift.Gifts, gift => evolving.Contains(gift.Individual.SpeciesId));
        foreach (var berry in SaveMigration.BerryIds) migrated.Berries[berry] = 20;
        return migrated;
    }

    private static Guid EvolvableGiftId(SaveData state)
    {
        using var rules = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Catalog.Value.DataRoot, "final", "catalog-rules.json")));
        var evolving = rules.RootElement.GetProperty("evolutionEdges").EnumerateArray()
            .Select(edge => edge.GetProperty("from").GetInt32()).ToHashSet();
        return state.Gift.Gifts.First(gift => evolving.Contains(gift.Individual.SpeciesId)).Individual.Id;
    }

    private static SaveData State(int speciesId, bool shiny = false, int care = 196_000)
    {
        var species = Catalog.Value.Species.Single(s => s.Id == speciesId);
        return new SaveData
        {
            SchemaVersion = 2, CatalogRevision = "test", BalanceRevision = "test", MigrationId = SaveMigration.MigrationId,
            Individuals = [new PokemonIndividual
            {
                SpeciesId = speciesId, CurrentSpeciesId = speciesId, FamilyRootId = species.FamilyRootId,
                EvolutionChainId = species.EvolutionChainId, ChainLength = 3, ReachedSpeciesIds = [speciesId],
                Nature = Nature.Hardy, IsShiny = shiny, CaughtAtUtc = DateTimeOffset.UtcNow,
                AccumulatedCarePoints = care, FirstTraitId = "gatherer", Nickname = "Партнёр"
            }],
            Settings = new(), Berries = SaveMigration.BerryIds.ToDictionary(id => id, _ => 20),
            Items = SaveMigration.ItemIds.ToDictionary(id => id, _ => 0),
            BerryRemainders = SaveMigration.BerryIds.ToDictionary(id => id, _ => new RationalRemainder())
        };
    }

    [Fact]
    public void PersonalCardShowsRetrainingRecipeOutcomesAndShopShortage() => WpfTestHost.Run(() =>
    {
        var data = State(1);
        var app = new AppState();
        app.Hydrate(data);
        var id = data.Individuals[0].Id;
        var vm = new CollectionViewModel(app.Collection, app.CareStateByIndividualId, new CareModeService(),
            [], [], species: Catalog.Value.Species, discoveredSpeciesIds: () => app.DiscoveredSpeciesIds);
        MenuSection? destination = null;
        (string Berry, int Required)? purchase = null;
        var view = new CollectionView(vm, app,
            new SpriteCache(Path.Combine(Path.GetTempPath(), "retraining-view", Guid.NewGuid().ToString("N"))),
            () => { }, () => { }, () => { }, () => null, () => false, () => true,
            previewRetraining: (individualId, slot) => new RetrainingService(Catalog.Value, data).Preview(individualId, slot),
            retrainIndividual: (_, _, _) => Task.FromResult(new RetrainingAction(false, "test")),
            navigate: section => destination = section,
            buyBerries: (berry, required) => purchase = (berry, required));
        Assert.True(view.OpenIndividual(id));
        WpfTestHost.Layout(view, 320, 420);
        Assert.Equal(Visibility.Visible, ((Border)view.FindName("DetailRetrainingSection")).Visibility);
        Assert.Contains("20 / 20", ((TextBlock)view.FindName("DetailRetrainingCostText")).Text);
        Assert.Contains("равный шанс", ((TextBlock)view.FindName("DetailRetrainingOutcomesText")).Text);
        Assert.True(((Button)view.FindName("DetailRetrainButton")).IsEnabled);
        data.Berries["rawst"] = 0;
        view.Refresh();
        Assert.False(((Button)view.FindName("DetailRetrainButton")).IsEnabled);
        ((Button)view.FindName("DetailRetrainingShopButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Equal(("rawst", 20), purchase);
        Assert.Equal(MenuSection.Shop, destination);
    });

    [Fact]
    public void ConfirmedPersonalCardRetrainingCallsRealProducer() => WpfTestHost.Run(() =>
    {
        var data = State(1);
        var app = new AppState();
        app.Hydrate(data);
        var id = data.Individuals[0].Id;
        var vm = new CollectionViewModel(app.Collection, app.CareStateByIndividualId, new CareModeService(),
            [], [], species: Catalog.Value.Species, discoveredSpeciesIds: () => app.DiscoveredSpeciesIds);
        Guid operation = Guid.Empty;
        var view = new CollectionView(vm, app,
            new SpriteCache(Path.Combine(Path.GetTempPath(), "retraining-confirm", Guid.NewGuid().ToString("N"))),
            () => { }, () => { }, () => { }, () => null, () => false, () => true,
            previewRetraining: (individualId, slot) => new RetrainingService(Catalog.Value, data).Preview(individualId, slot),
            retrainIndividual: (individualId, slot, operationId) =>
            {
                operation = operationId;
                return Task.FromResult(new RetrainingService(Catalog.Value, data).Retrain(individualId, slot, operationId));
            });
        Assert.True(view.OpenIndividual(id));
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) =>
        {
            var dialog = VisibleRetrainingConfirmation();
            if (dialog is null) return;
            timer.Stop();
            Assert.Contains("20", ((TextBlock)dialog.FindName("BodyText")).Text);
            ((Button)dialog.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        };
        timer.Start();
        ((Button)view.FindName("DetailRetrainButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        timer.Stop();
        Assert.NotEqual(Guid.Empty, operation);
        Assert.Equal(0, data.Berries["rawst"]);
        Assert.True(data.Metrics.ContainsKey("special:trophy-special-retrain"));
    });

    [Fact]
    public void EeveeUsesOneBerryForEveryActualBranchAndPreservesIdentity()
    {
        var data = State(133, shiny: true);
        var pet = data.Individuals[0];
        var preview = new EvolutionService(Catalog.Value, data).Preview(pet.Id);
        Assert.Equal(8, preview.Branches.Count);
        Assert.All(preview.Branches, branch => Assert.True(branch.Available));
        Assert.Equal("oran", preview.BerryId);
        Assert.Equal(6, preview.BerryCost);
        var action = new EvolutionService(Catalog.Value, data).Evolve(pet.Id, null, Guid.NewGuid());
        Assert.True(action.Success, action.Message);
        Assert.Contains(pet.CurrentSpeciesId, preview.Branches.Select(b => b.SpeciesId));
        Assert.Equal(pet.Id, data.Individuals[0].Id);
        Assert.Equal(196_000, pet.AccumulatedCarePoints);
        Assert.Equal("Партнёр", pet.Nickname);
        Assert.True(pet.IsShiny);
        Assert.Equal(133, pet.FamilyRootId);
        Assert.Equal(Nature.Hardy, pet.Nature);
        Assert.Equal("gatherer", pet.FirstTraitId);
        Assert.Contains(pet.CurrentSpeciesId, data.ShinySpeciesIds);
        Assert.Equal(14, data.Berries["oran"]);
        Assert.Single(data.EvolutionTransitions);
    }

    [Fact]
    public void ShinyWithoutNextAssetCannotFallBackOrSkip()
    {
        var data = State(651, shiny: true);
        var pet = data.Individuals[0];
        var preview = new EvolutionService(Catalog.Value, data).Preview(pet.Id);
        Assert.False(preview.Branches.Single(b => b.SpeciesId == 652).Available);
        var action = new EvolutionService(Catalog.Value, data).Evolve(pet.Id, null, Guid.NewGuid());
        Assert.False(action.Success);
        Assert.Equal(651, pet.CurrentSpeciesId);
        Assert.True(pet.IsShiny);
        Assert.Equal(20, data.Berries[preview.BerryId]);
    }

    [Fact]
    public void HighCareAdvancesOnlyOneCanonicalEdgePerConfirmedOperation()
    {
        var data = State(1);
        var id = data.Individuals[0].Id;
        Assert.True(new EvolutionService(Catalog.Value, data).Evolve(id, null, Guid.NewGuid()).Success);
        Assert.Equal(2, data.Individuals[0].CurrentSpeciesId);
        Assert.Equal(EvolutionStage.Stage2, data.Individuals[0].CurrentStage);
        Assert.Equal(196_000, data.Individuals[0].AccumulatedCarePoints);
        Assert.True(new EvolutionService(Catalog.Value, data).Evolve(id, null, Guid.NewGuid()).Success);
        Assert.Equal(3, data.Individuals[0].CurrentSpeciesId);
        Assert.Equal(new[] { 1, 2, 3 }, data.Individuals[0].ReachedSpeciesIds);
    }

    [Fact]
    public void SecondStepRequires196kEvenAfterTheFirstStep()
    {
        var data = State(1, care: 70_000);
        var id = data.Individuals[0].Id;
        Assert.True(new EvolutionService(Catalog.Value, data).Evolve(id, null, Guid.NewGuid()).Success);
        Assert.False(new EvolutionService(Catalog.Value, data).Evolve(id, null, Guid.NewGuid()).Success);
        Assert.Equal(2, data.Individuals[0].CurrentSpeciesId);
        data.Individuals[0].AccumulatedCarePoints = 196_000;
        Assert.True(new EvolutionService(Catalog.Value, data).Evolve(id, null, Guid.NewGuid()).Success);
        Assert.Equal(3, data.Individuals[0].CurrentSpeciesId);
    }

    [Fact]
    public void MeaninglessChoiceNeedsNoStoneButRealChoiceDoes()
    {
        var single = State(1);
        Assert.False(new EvolutionService(Catalog.Value, single).Evolve(single.Individuals[0].Id, 3, Guid.NewGuid()).Success);
        Assert.Equal(20, single.Berries["rawst"]);
        Assert.True(new EvolutionService(Catalog.Value, single).Evolve(single.Individuals[0].Id, 2, Guid.NewGuid()).Success);
        Assert.Equal(0, single.Items["evolution-stone"]);
        var branching = State(133);
        var id = branching.Individuals[0].Id;
        Assert.False(new EvolutionService(Catalog.Value, branching).Evolve(id, 134, Guid.NewGuid()).Success);
        Assert.Equal(20, branching.Berries["oran"]);
        branching.Items["evolution-stone"] = 1;
        Assert.True(new EvolutionService(Catalog.Value, branching).Evolve(id, 134, Guid.NewGuid()).Success);
        Assert.Equal(134, branching.Individuals[0].CurrentSpeciesId);
        Assert.Equal(0, branching.Items["evolution-stone"]);
    }

    [Fact]
    public void ReplayBindsIndividualAndChoiceAndNeverSpendsTwice()
    {
        var data = State(133);
        var other = State(133).Individuals[0];
        data.Individuals.Add(other);
        data.Items["evolution-stone"] = 1;
        var id = data.Individuals[0].Id;
        var operation = Guid.NewGuid();
        var service = new EvolutionService(Catalog.Value, data);
        Assert.True(service.Evolve(id, 134, operation).Success);
        Assert.True(service.Evolve(id, 134, operation).Repeated);
        Assert.True(new EvolutionService(Catalog.Value, JsonSaveStore.Clone(data)).Evolve(id, 134, operation).Repeated);
        Assert.False(service.Evolve(other.Id, 134, operation).Success);
        Assert.False(service.Evolve(id, 135, operation).Success);
        Assert.Equal(14, data.Berries["oran"]);
        Assert.Equal(0, data.Items["evolution-stone"]);
    }

    [Fact]
    public void BusyReadyNestAndInsufficientCareRefuseBeforeSpending()
    {
        var data = State(1, care: 69_999);
        var id = data.Individuals[0].Id;
        var service = new EvolutionService(Catalog.Value, data);
        Assert.False(service.Evolve(id, null, Guid.NewGuid()).Success);
        data.Individuals[0].AccumulatedCarePoints = 70_000;
        data.Nest = new NurseryState { Status = "ready", ParticipantIds = [id], Child = data.Individuals[0] };
        Assert.False(service.Evolve(id, null, Guid.NewGuid()).Success);
        data.Nest = null;
        data.Expedition = new ExpeditionState { Status = "running", ParticipantIds = [id] };
        Assert.False(service.Evolve(id, null, Guid.NewGuid()).Success);
        Assert.Equal(20, data.Berries["rawst"]);
        Assert.Equal(1, data.Individuals[0].CurrentSpeciesId);
    }

    [Fact]
    public void LegacyStageMutationCannotBypassV2CommitPath()
    {
        var live = new AppState();
        live.Hydrate(State(1));
        var individual = live.Collection.Individuals[0];
        Assert.False(live.TryEvolve(individual));
        Assert.Equal(1, individual.CurrentSpeciesId);
    }

    [Fact]
    public void PersonalCardShowsCanonicalCostBranchesAndLiveBusyAtCompactWidth() => WpfTestHost.Run(() =>
    {
        var data = State(133);
        var app = new AppState();
        app.Hydrate(data);
        var id = data.Individuals[0].Id;
        var vm = new CollectionViewModel(app.Collection, app.CareStateByIndividualId, new CareModeService(),
            [], [], species: Catalog.Value.Species, discoveredSpeciesIds: () => app.DiscoveredSpeciesIds);
        MenuSection? destination = null;
        (string Berry, int Required)? purchase = null;
        var view = new CollectionView(vm, app,
            new SpriteCache(Path.Combine(Path.GetTempPath(), "evolution-view", Guid.NewGuid().ToString("N"))),
            () => { }, () => { }, () => { }, () => null, () => false, () => true,
            previewEvolution: individualId => new EvolutionService(Catalog.Value, data).Preview(individualId),
            evolveIndividual: (_, _, _) => Task.FromResult(new EvolutionAction(false, "test")),
            navigate: section => destination = section,
            buyBerries: (berry, required) => purchase = (berry, required));
        Assert.True(view.OpenIndividual(id));
        WpfTestHost.Layout(view, 320, 420);
        var card = (Border)view.FindName("DetailCard");
        Assert.True(card.ActualWidth <= 320);
        Assert.Contains("oran: 20 / 6", ((TextBlock)view.FindName("DetailEvolutionCostText")).Text);
        var picker = (ComboBox)view.FindName("DetailEvolutionBranchPicker");
        Assert.True(picker.TranslatePoint(new Point(picker.ActualWidth, 0), card).X <= card.ActualWidth);
        Assert.Equal(9, picker.Items.Count);
        picker.SelectedIndex = 1;
        Assert.Contains("камень", ((TextBlock)view.FindName("DetailEvolutionReasonText")).Text);
        picker.SelectedIndex = 0;
        data.Berries["oran"] = 2;
        view.Refresh();
        Assert.Contains("ягод", ((TextBlock)view.FindName("DetailEvolutionReasonText")).Text);
        ((Button)view.FindName("DetailEvolutionShopButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Equal(("oran", 6), purchase);
        Assert.Equal(MenuSection.Shop, destination);
        data.Berries["oran"] = 20;
        data.Nest = new NurseryState { Status = "ready", ParticipantIds = [id], Child = data.Individuals[0] };
        view.Refresh();
        Assert.Contains("занят", ((TextBlock)view.FindName("DetailEvolutionReasonText")).Text);
        Assert.False(((Button)view.FindName("DetailEvolveButton")).IsEnabled);
    });

    [Fact]
    public void CancelledCardConfirmationDoesNotInvokeCommitOrSpendBerries() => WpfTestHost.Run(() =>
    {
        var data = State(1);
        var app = new AppState();
        app.Hydrate(data);
        var id = data.Individuals[0].Id;
        var vm = new CollectionViewModel(app.Collection, app.CareStateByIndividualId, new CareModeService(),
            [], [], species: Catalog.Value.Species, discoveredSpeciesIds: () => app.DiscoveredSpeciesIds);
        var commits = 0;
        var view = new CollectionView(vm, app,
            new SpriteCache(Path.Combine(Path.GetTempPath(), "evolution-cancel", Guid.NewGuid().ToString("N"))),
            () => { }, () => { }, () => { }, () => null, () => false, () => true,
            previewEvolution: individualId => new EvolutionService(Catalog.Value, data).Preview(individualId),
            evolveIndividual: (_, _, _) => { commits++; return Task.FromResult(new EvolutionAction(true, "test")); });
        Assert.True(view.OpenIndividual(id));
        WpfTestHost.Layout(view, 420, 420);
        var opened = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) =>
        {
            var dialog = Application.Current.Windows.OfType<ConfirmDialog>().FirstOrDefault();
            if (dialog is null) return;
            opened = true;
            timer.Stop();
            dialog.Close();
        };
        timer.Start();
        ((Button)view.FindName("DetailEvolveButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        timer.Stop();
        Assert.True(opened);
        Assert.Equal(0, commits);
        Assert.Equal(20, data.Berries["rawst"]);
        Assert.Equal(id, view.SelectedIndividualId);
    });

    [Fact]
    public async Task FailedCommitLeavesPublishedIndividualAndBerriesUntouched()
    {
        var live = new AppState();
        live.Hydrate(State(133));
        var id = live.Collection.Individuals[0].Id;
        var operation = Guid.NewGuid();
        var path = Path.Combine(Path.GetTempPath(), "evolution-" + Guid.NewGuid().ToString("N") + ".json");
        var writer = new BackgroundSaveWriter(new JsonSaveStore(path), _ => throw new IOException("synthetic failure"));
        EvolutionAction? proposed = null;
        await Assert.ThrowsAsync<IOException>(() => live.CommitAsync(writer, next =>
            proposed = new EvolutionService(Catalog.Value, next).Evolve(id, null, operation), () => 0));
        Assert.Equal(133, live.Collection.Individuals[0].CurrentSpeciesId);
        Assert.Equal(20, live.Shop.Berries["oran"]);
        EvolutionAction? committed = null;
        await live.CommitAsync(new BackgroundSaveWriter(new JsonSaveStore(path)), next =>
            committed = new EvolutionService(Catalog.Value, next).Evolve(id, null, operation), () => 0);
        Assert.Equal(proposed!.SpeciesId, committed!.SpeciesId);
        Assert.Equal(14, live.Shop.Berries["oran"]);
        File.Delete(path);
    }

    [Fact]
    public async Task OpenedMigrationGiftCanCommitCanonicalEvolutionAndKeepIdentity()
    {
        var migrated = MigrationWithEvolvableGift();
        var id = EvolvableGiftId(migrated);
        var original = migrated.Gift.Gifts.Single(g => g.Individual.Id == id).Individual;
        original.Nickname = "Подарок";
        original.IsFavorite = true;
        original.TotalXp = 123;
        original.SecondTraitId = original.FirstTraitId == "collector" ? "mentor" : "collector";
        Assert.True(original.ChainLength >= 2);
        var path = Path.Combine(Path.GetTempPath(), "evolution-gift-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSaveStore(path);
            var writer = new BackgroundSaveWriter(store);
            var live = new AppState(); live.Hydrate(migrated);
            var encounter = new EncounterService(Catalog.Value);
            var ballId = Guid.NewGuid();
            await live.CommitAsync(writer, next =>
            {
                encounter.ReserveGift(next, ballId, id);
                encounter.Open(next, ballId);
                next.Individuals.Single(x => x.Id == id).AccumulatedCarePoints = 70_000;
            }, () => 0);
            var opened = live.Collection.Individuals.Single(x => x.Id == id);
            Assert.Same(opened, live.Collection.Individuals.Single(x => x.Id == id));
            var preview = new EvolutionService(Catalog.Value, live.ToSaveData(0)).Preview(id);
            var heroReady = live.IsReadyToEvolve(opened, preview);
            Assert.Contains(preview.Branches, branch => branch.Available);
            var action = new EvolutionAction(false, "not run");
            await live.CommitAsync(writer, next => action = new EvolutionService(Catalog.Value, next).Evolve(id, null, Guid.NewGuid()), () => 0);
            Assert.True(heroReady);
            Assert.True(action.Success, action.Message);
            var saved = store.Load()!;
            var evolved = saved.Individuals.Single(x => x.Id == id);
            Assert.Equal(original.Id, evolved.Id);
            Assert.Equal(original.Nature, evolved.Nature);
            Assert.Equal(original.IsShiny, evolved.IsShiny);
            Assert.Equal(original.CaughtAtUtc, evolved.CaughtAtUtc);
            Assert.Equal(original.FirstTraitId, evolved.FirstTraitId);
            Assert.Equal(original.SecondTraitId, evolved.SecondTraitId);
            Assert.Equal(original.Nickname, evolved.Nickname);
            Assert.Equal(original.IsFavorite, evolved.IsFavorite);
            Assert.Equal(original.TotalXp, evolved.TotalXp);
            Assert.Equal(original.FamilyRootId, evolved.FamilyRootId);
            Assert.Equal(70_000, evolved.AccumulatedCarePoints);
            Assert.Equal(EvolutionStage.Stage2, evolved.CurrentStage);
            Assert.Equal(new[] { original.SpeciesId, action.SpeciesId!.Value }, evolved.ReachedSpeciesIds);
            Assert.Contains(action.SpeciesId.Value, saved.DiscoveredSpeciesIds);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task AlreadySavedV2OpenedGiftWithOldLengthCanEvolveAfterRoundTrip()
    {
        var migrated = MigrationWithEvolvableGift();
        var id = EvolvableGiftId(migrated);
        var encounter = new EncounterService(Catalog.Value);
        var ballId = Guid.NewGuid();
        encounter.ReserveGift(migrated, ballId, id);
        encounter.Open(migrated, ballId);
        migrated.Individuals.Single(x => x.Id == id).AccumulatedCarePoints = 70_000;
        var old = JsonNode.Parse(JsonSaveStore.Serialize(migrated))!;
        old["Individuals"]!.AsArray().Single(x => x!["Id"]!.GetValue<Guid>() == id)!["ChainLength"] = 1;
        old["Gift"]!["Gifts"]!.AsArray().Single(x => x!["Individual"]!["Id"]!.GetValue<Guid>() == id)!["Individual"]!["ChainLength"] = 1;
        old["Encounters"]!.AsArray().Single(x => x!["Id"]!.GetValue<Guid>() == ballId)!["Individual"]!["ChainLength"] = 1;
        var path = Path.Combine(Path.GetTempPath(), "evolution-old-gift-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSaveStore(path);
            store.SaveSerialized(old.ToJsonString());
            var loaded = store.Load()!;
            Assert.Equal(1, loaded.Individuals.Single(x => x.Id == id).ChainLength);
            var live = new AppState(); live.Hydrate(loaded);
            var opened = live.Collection.Individuals.Single(x => x.Id == id);
            var preview = new EvolutionService(Catalog.Value, live.ToSaveData(0)).Preview(id);
            var heroReady = live.IsReadyToEvolve(opened, preview);
            var operationId = Guid.NewGuid();
            await Assert.ThrowsAsync<IOException>(() => live.CommitAsync(
                new BackgroundSaveWriter(store, _ => throw new IOException("synthetic gift write failure")),
                next => new EvolutionService(Catalog.Value, next).Evolve(id, null, operationId), () => 0));
            Assert.Equal(1, live.Collection.Individuals.Single(x => x.Id == id).ChainLength);
            Assert.Equal(1, store.Load()!.Individuals.Single(x => x.Id == id).ChainLength);
            EvolutionAction? action = null;
            await live.CommitAsync(new BackgroundSaveWriter(store), next =>
                action = new EvolutionService(Catalog.Value, next).Evolve(id, null, operationId), () => 0);
            Assert.True(heroReady);
            Assert.True(action!.Success, action.Message);
            var roundTrip = store.Load()!;
            Assert.Equal(id, roundTrip.Individuals.Single(x => x.Id == id).Id);
            Assert.Equal(EvolutionStage.Stage2, roundTrip.Individuals.Single(x => x.Id == id).CurrentStage);
            Assert.True(roundTrip.Individuals.Single(x => x.Id == id).ChainLength >= 2);
            Assert.Equal(1, roundTrip.Gift.Gifts.Single(g => g.Individual.Id == id).Individual.ChainLength);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void StandaloneCanonicalGiftRootStillOpensButHasNoEvolution()
    {
        var encounter = new EncounterService(Catalog.Value);
        var solo = Catalog.Value.Species.First(species =>
            Catalog.Value.CanIssueDirectly(species.Id, false) && encounter.DirectRootChainLength(species.Id) == 1);
        var other = Catalog.Value.Species.First(species =>
            species.Id != solo.Id && Catalog.Value.CanIssueDirectly(species.Id, false));
        PokemonIndividual Gift(CatalogSpecies species) => new()
        {
            SpeciesId = species.Id, CurrentSpeciesId = species.Id, FamilyRootId = species.FamilyRootId,
            EvolutionChainId = species.EvolutionChainId, ChainLength = encounter.DirectRootChainLength(species.Id),
            Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow,
            AcquisitionSource = "update-gift", ReachedSpeciesIds = [species.Id], FirstTraitId = "gatherer",
            AccumulatedCarePoints = 196_000
        };
        var gift = Gift(solo);
        var state = State(1);
        state.Gift.Gifts = [new ReservedGift { Individual = gift }, new ReservedGift { Individual = Gift(other) }];
        var ballId = Guid.NewGuid();
        encounter.ReserveGift(state, ballId, gift.Id);
        encounter.Open(state, ballId);
        JsonSaveStore.ValidateData(state);
        var preview = new EvolutionService(Catalog.Value, state).Preview(gift.Id);
        Assert.Empty(preview.Branches);
        Assert.False(new AppState().IsReadyToEvolve(gift, preview));
        var before = state.Berries.ToDictionary(pair => pair.Key, pair => pair.Value);
        Assert.False(new EvolutionService(Catalog.Value, state).Evolve(gift.Id, null, Guid.NewGuid()).Success);
        Assert.Equal(before, state.Berries);
        Assert.Equal(1, gift.ChainLength);
    }
}
