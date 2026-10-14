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

namespace PokeCorner.App.Tests.GlobalUpdate;

public sealed class EvolutionTests
{
    private static readonly Lazy<CatalogPackage> Catalog = new(() => CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data")));

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
}
