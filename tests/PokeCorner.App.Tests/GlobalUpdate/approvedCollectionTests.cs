using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using PokeCorner.Core.Persistence;

namespace PokeCorner.App.Tests.GlobalUpdate;

public sealed class ApprovedCollectionTests
{
    private static PokemonIndividual Pet(int id, bool shiny = false) => new()
    {
        SpeciesId = id, CurrentSpeciesId = id, FamilyRootId = id, ReachedSpeciesIds = [id],
        EvolutionChainId = id, ChainLength = 1, Nature = Nature.Hardy, IsShiny = shiny,
        CaughtAtUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
    };
    private static CollectionViewModel Vm(AppState state) => new(state.Collection, state.CareStateByIndividualId,
        new CareModeService(), [], [], species: [new CatalogSpecies { Id = 1, DisplayName = "Бульбазавр", Types = ["grass"], NormalAvailable = true, ShinyAvailable = true }]);

    [Fact]
    public void NewShinyOnlyDoesNotOpenOrdinaryMode()
    {
        var state = new AppState(); state.Collection.Add(Pet(1, true));
        var normal = Vm(state).GetSpeciesEntries().Single();
        Assert.False(normal.IsDiscovered);
        Assert.Null(normal.DisplayName);
        Assert.Empty(normal.OwnedIndividuals);
    }

    [Theory]
    [InlineData(1000, 760)]
    [InlineData(840, 650)]
    public void ApprovedSelectorsAndWholeCardAreAvailableAtBothSizes(int width, int height) => WpfTestHost.Run(() =>
    {
        var state = new AppState(); var pet = Pet(1); pet.Nickname = "Очень длинная кличка"; state.Collection.Add(pet);
        var view = new CollectionView(Vm(state), state, new SpriteCache(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())),
            () => {}, () => {}, () => {}, () => null, () => false, () => false);
        WpfTestHost.Layout(view, width - 190, height - 150);
        Assert.NotNull(view.FindName("SpeciesShinyToggle"));
        Assert.NotNull(view.FindName("DuplicatesFilterToggle"));
        Assert.NotNull(view.FindName("IndividualTypeCombo"));
        Assert.True(view.OpenIndividual(pet.Id));
        Assert.NotNull(view.FindName("DetailCareBoosterButton"));
        Assert.NotNull(view.FindName("DetailXpPotionButton"));
        WpfTestHost.Layout(view, width - 190, height - 150);
        Assert.True(((Border)view.FindName("DetailCard")).ActualWidth <= width - 190);
        var scroll = (ScrollViewer)view.FindName("DetailScroll");
        foreach (var action in new[] { "DetailSelectButton", "DetailCareBoosterButton", "DetailXpPotionButton", "DetailRehomeButton" })
        {
            var button = (Button)view.FindName(action); button.BringIntoView(); WpfTestHost.Layout(view, width - 190, height - 150);
            var top = button.TranslatePoint(new Point(0, 0), view).Y;
            Assert.InRange(top, 0, height - 150 - button.ActualHeight);
        }
    });

    [Fact]
    public void ModesSelectOnlyTheirOwnIndividualsAndHistorySurvivesRemoval()
    {
        var state = new AppState(); var normal = Pet(1); var shiny = Pet(1, true); state.Collection.Add(normal); state.Collection.Add(shiny);
        var normalHistory = new HashSet<int> { 1 }; var shinyHistory = new HashSet<int> { 1 };
        var vm = new CollectionViewModel(state.Collection, state.CareStateByIndividualId, new CareModeService(), [], [],
            species: [new CatalogSpecies { Id = 1, DisplayName = "Открытый", Types = ["grass"], NormalAvailable = true, ShinyAvailable = false },
                new CatalogSpecies { Id = 2, DisplayName = "Секретный", Types = ["fire"], Rarity = "Rare" }],
            normalSpeciesIds: () => normalHistory, shinySpeciesIds: () => shinyHistory);
        Assert.Equal(normal.Id, vm.GetSpeciesEntries().First().OwnedIndividuals.Single().IndividualId);
        Assert.Equal(shiny.Id, vm.GetSpeciesEntries(true).First().OwnedIndividuals.Single().IndividualId);
        Assert.False(vm.GetSpeciesEntries(true).First().HasSilhouette); // Unsupported shiny stays represented.
        var closed = vm.GetSpeciesEntries(true).Last();
        Assert.False(CollectionViewModel.MatchesSpeciesSearch(closed, "Секрет"));
        Assert.Empty(CollectionViewModel.FilterSpecies([closed], "", "fire", "Rare", false));
        state.Collection.Release(shiny.Id);
        Assert.True(vm.GetSpeciesEntries(true).First().IsDiscovered); Assert.Empty(vm.GetSpeciesEntries(true).First().OwnedIndividuals);
    }

    [Fact]
    public void EightHundredIndividualsUseCurrentSpeciesForDuplicatesAndAndEveryFilter() => WpfTestHost.Run(() =>
    {
        var state = new AppState();
        for (var n = 0; n < 800; n++) { var p = Pet(n / 2 + 1, n % 2 == 1); p.IsFavorite = n % 3 == 0; p.AccumulatedCarePoints = n; p.Nickname = "Питомец " + n; state.Collection.Add(p); }
        var vm = new CollectionViewModel(state.Collection, state.CareStateByIndividualId, new CareModeService(), [], [], levelFor: p => p.AccumulatedCarePoints % 40 + 1);
        var entries = vm.GetEntries(DateTimeOffset.UtcNow);
        Assert.Equal(2, CollectionViewModel.FilterIndividuals(entries, "#0001", null, null, false, false, false, false).Count);
        var filtered = CollectionViewModel.FilterIndividuals(entries, "", null, null, false, true, true, true, PokedexSortOrder.Level);
        Assert.All(filtered, e => { Assert.True(e.IsFavorite); Assert.True(e.IsShiny); });
        Assert.Equal(133, filtered.Count);
        Assert.Equal(filtered.Select(e => e.Level).OrderDescending(), filtered.Select(e => e.Level));
        Assert.Equal(799, CollectionViewModel.ApplySort(entries, PokedexSortOrder.Care).First().CarePoints);
        var view = new CollectionView(vm, state, new SpriteCache(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())), () => {}, () => {}, () => {}, () => null, () => false, () => false);
        var grid = (ItemsControl)view.FindName("PokedexGrid"); Assert.Equal(16, grid.Items.Count);
        ((Button)view.FindName("PokedexNextPageButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        var before = grid.ItemsSource; var page = ((TextBlock)view.FindName("PokedexPageText")).Text; view.Refresh();
        Assert.Same(before, grid.ItemsSource); Assert.Equal(page, ((TextBlock)view.FindName("PokedexPageText")).Text);
    });

    [Fact]
    public void CanonicalEeveeGraphHasEightParallelChildrenAndFactsArePreparedOnly()
    {
        var catalog = CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data")); var presentation = new CollectionPresentation(catalog);
        var state = new AppState(); var eevee = Pet(133); state.Collection.Add(eevee);
        var vm = new CollectionViewModel(state.Collection, state.CareStateByIndividualId, new CareModeService(), [], [], species: catalog.Species, presentation: presentation);
        var graph = vm.GetEntry(eevee.Id, DateTimeOffset.UtcNow)!.EvolutionStages;
        Assert.Single(graph.Where(n => n.Depth == 0)); Assert.Equal(8, graph.Count(n => n.Depth == 1));
        Assert.All(graph.Where(n => n.Depth == 1), n => { Assert.Null(n.SpeciesName); Assert.Null(n.SpeciesId); });
        Assert.NotNull(CollectionPresentation.Fact(1)); Assert.Null(CollectionPresentation.Fact(1025));
        Assert.StartsWith("Эволюция из", presentation.WhereFind(134, false, new HashSet<int>()));
        Assert.DoesNotContain("Eevee", presentation.WhereFind(134, false, new HashSet<int>()));
        Assert.Contains("Питомник", presentation.WhereFind(1, false, new HashSet<int>()));
    }

    [Fact]
    public void FailedItemCommitKeepsInventoryAndPetAndRetriesSameOperation() => WpfTestHost.Run(() =>
    {
        var state = new AppState(); var pet = Pet(1); state.Collection.Add(pet); state.Shop.Items["care-booster"] = 1;
        var operations = new List<Guid>();
        var view = new CollectionView(Vm(state), state, new SpriteCache(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())), () => {}, () => {}, () => {}, () => null, () => false, () => false,
            confirmAsync: (_, body, _) => { Assert.Contains("потрачен 1", body); return Task.FromResult(true); },
            previewItem: (_, item) => new(item, "Бустер ухода", state.Shop.Items.GetValueOrDefault(item), "Уход для эволюции", null),
            useItem: (_, _, operation) => { operations.Add(operation); return Task.FromException<PurchaseResult>(new IOException("refused item write")); });
        view.OpenIndividual(pet.Id); var button = (Button)view.FindName("DetailCareBoosterButton");
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Contains("refused item write", ((TextBlock)view.FindName("DetailErrorText")).Text);
        Assert.Equal(1, state.Shop.Items["care-booster"]); Assert.Equal(0, pet.AccumulatedCarePoints);
        Assert.True(button.IsEnabled); button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Equal(2, operations.Count); Assert.Equal(operations[0], operations[1]);
    });

    [Fact]
    public async Task PendingConfirmationBlocksDuplicateAndCannotApplyToAnotherCard()
    {
        var state = new AppState(); var one = Pet(1); var two = Pet(1); state.Collection.Add(one); state.Collection.Add(two);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var confirmations = 0; var uses = 0; CollectionView? view = null;
        WpfTestHost.Run(() =>
        {
            view = new CollectionView(Vm(state), state, new SpriteCache(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())), () => {}, () => {}, () => {}, () => null, () => false, () => false,
                confirmAsync: (_, _, _) => { confirmations++; return gate.Task; }, previewItem: (_, item) => new(item, item, 1, "effect", null),
                useItem: (_, _, _) => { uses++; return Task.FromResult(new PurchaseResult(true, "ok")); });
            view.OpenIndividual(one.Id); var button = (Button)view.FindName("DetailCareBoosterButton");
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.Equal(1, confirmations); Assert.False(button.IsEnabled); view.OpenIndividual(two.Id);
        });
        gate.SetResult(true); await view!.Dispatcher.InvokeAsync(() => {}, DispatcherPriority.ContextIdle).Task;
        Assert.Equal(0, uses);
    }

    [Fact]
    public void StoneSelectsExactTargetAndTurningItOffForcesCanonicalRandom() => WpfTestHost.Run(() =>
    {
        var state = new AppState(); var pet = Pet(1); state.Collection.Add(pet);
        var selected = new List<int?>();
        var view = new CollectionView(Vm(state), state, new SpriteCache(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())), () => {}, () => {}, () => {}, () => null, () => false, () => false,
            previewEvolution: _ => new([new(2, "SecretOne", true, null), new(3, "SecretTwo", true, null)], "oran", 6, 20, 1, 100, 1, false, null),
            evolveIndividual: (_, branch, _) => { selected.Add(branch); return Task.FromResult(new EvolutionAction(true, "ok")); },
            confirmAsync: (_, _, _) => Task.FromResult(true));
        view.OpenIndividual(pet.Id); var stone = (CheckBox)view.FindName("DetailEvolutionStoneToggle"); var picker = (ComboBox)view.FindName("DetailEvolutionBranchPicker");
        Assert.False(picker.IsEnabled); Assert.DoesNotContain("Secret", string.Join("|", picker.Items.Cast<ComboBoxItem>().Select(i => i.Content)));
        stone.IsChecked = true; Assert.True(picker.IsEnabled); picker.SelectedIndex = 2;
        ((Button)view.FindName("DetailEvolveButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Equal(3, selected.Single());
        stone.IsChecked = false; picker.SelectedIndex = 1; // stale selection is ignored without stone.
        ((Button)view.FindName("DetailEvolveButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Equal(new int?[] { 3, null }, selected);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrUnreadableArtworkKeepsExactIndividualReachable(bool corrupt) => WpfTestHost.Run(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "collection-art-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "pet.gif"); if (corrupt) File.WriteAllBytes(path, [1, 2, 3, 4]);
            var state = new AppState(); var pet = Pet(1); state.Collection.Add(pet);
            var view = new CollectionView(Vm(state), state, new SpriteCache(directory), () => {}, () => {}, () => {}, () => null, () => false, () => false,
                getSpeciesAssetPath: (_, _) => path);
            var card = Assert.IsType<PokedexCard>(Assert.Single(((ItemsControl)view.FindName("PokedexGrid")).Items));
            Assert.Null(card.SpriteImage); Assert.True(view.OpenIndividual(pet.Id));
            Assert.Equal(pet.Id, view.SelectedIndividualId);
            Assert.Equal(Visibility.Visible, ((FrameworkElement)view.FindName("DetailSpritePlaceholder")).Visibility);
        }
        finally { Directory.Delete(directory, true); }
    });
}
