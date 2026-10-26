using PokeCorner.App.Popup;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;
using PokeCorner.Core;
using PokeCorner.App.Tests.Theme;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PokeCorner.App.Tests.GlobalUpdate;

public class PokedexTests
{
    [Fact]
    public void PackagedRouteHintsCoverTheCanonicalSpecies()
    {
        var catalog = CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data"));
        var hints = catalog.GetRouteHints();
        Assert.Equal(catalog.Species.Count, hints.Count);
        Assert.Equal("Лесная роща", hints[1]);
    }

    [Fact]
    public void ClosedPackagedSpeciesUsesAnActualVerifiedSilhouette() => WpfTestHost.Run(() =>
    {
        var catalog = CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data"));
        var state = new AppState();
        var vm = new CollectionViewModel(state.Collection, state.CareStateByIndividualId, new CareModeService(),
            [], [], species: catalog.Species, routeHints: catalog.GetRouteHints());
        var view = new CollectionView(vm, state,
            new SpriteCache(Path.Combine(Path.GetTempPath(), "pokedex-test", Guid.NewGuid().ToString("N"))),
            () => { }, () => { }, () => { }, () => null, () => false, () => false,
            getSpeciesAssetPath: (id, shiny) => catalog.GetAssetPath(id, shiny));
        ((TabControl)((Grid)view.Content).Children[0]).SelectedIndex = 1;
        ((Button)view.FindName("SpeciesModeButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        WpfTestHost.Layout(view, 560, 480);
        var card = ((ListBox)view.FindName("SpeciesGrid")).Items[0];
        Assert.Equal(Visibility.Visible, card.GetType().GetProperty("SilhouetteVisibility")!.GetValue(card));
        Assert.Equal("Неизвестный вид", card.GetType().GetProperty("NameText")!.GetValue(card));
        Assert.True(((BitmapSource)card.GetType().GetProperty("SpriteImage")!.GetValue(card)!).PixelWidth > 0);
    });

    [Fact]
    public void ClosedSpeciesExposeOnlyPublicSearchAndSortFields()
    {
        var vm = CreateVm();
        var closed = vm.GetSpeciesEntries().Single(e => e.SpeciesId == 2);
        Assert.False(closed.IsDiscovered);
        Assert.Null(closed.DisplayName);
        Assert.Null(closed.DescriptionRu);
        Assert.Empty(closed.Types);
        Assert.Null(closed.Rarity);
        Assert.True(closed.HasSilhouette);
        Assert.False(CollectionViewModel.MatchesSpeciesSearch(closed, "secret"));
        Assert.True(CollectionViewModel.MatchesSpeciesSearch(closed, "#0002"));
        Assert.True(CollectionViewModel.MatchesSpeciesSearch(closed, "роща"));
        Assert.DoesNotContain(closed, CollectionViewModel.FilterSpecies(vm.GetSpeciesEntries(), "", "fire", null, false));
    }

    [Fact]
    public void UnsupportedClosedSpeciesHasNoSilhouetteAndDiscoverySurvivesRelease()
    {
        var vm = CreateVm();
        Assert.False(vm.GetSpeciesEntries().Single(e => e.SpeciesId == 3).HasSilhouette);
        Assert.True(vm.GetSpeciesEntries().Single(e => e.SpeciesId == 1).IsDiscovered);
        Assert.Empty(vm.GetSpeciesEntries().Single(e => e.SpeciesId == 1).OwnedIndividuals);
    }

    [Fact]
    public void MultipleOwnedIndividualsRequireStableExplicitChoice()
    {
        var collection = new PokemonCollection();
        var first = new PokemonIndividual { SpeciesId = 1, EvolutionChainId = 1, ChainLength = 1, Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow };
        var second = new PokemonIndividual { SpeciesId = 1, EvolutionChainId = 1, ChainLength = 1, Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow };
        collection.Add(first);
        collection.Add(second);
        var vm = CreateVm(collection);
        Assert.Null(vm.ResolveDirectIndividual(1));
        Assert.Equal(new[] { first.Id, second.Id }, vm.GetSpeciesEntries().Single(e => e.SpeciesId == 1).OwnedIndividuals.Select(i => i.IndividualId));
        Assert.Equal(second.Id, vm.GetEntry(second.Id, DateTimeOffset.UtcNow)?.IndividualId);
    }

    [Fact]
    public void CosmeticDisplayOverrideDoesNotMoveTheOwnedIndividualToAnEarlierSpecies()
    {
        var collection = new PokemonCollection();
        var individual = new PokemonIndividual { SpeciesId = 1, EvolutionChainId = 1, ChainLength = 2,
            CurrentStage = EvolutionStage.Stage2, DisplayStageOverride = 0, Nature = Nature.Hardy,
            IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow };
        collection.Add(individual);
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(),
            [new SpeciesEvolutionChain(1, 1, 45, 2, [1, 2], ["Known", "Secret"])], [],
            species: [new CatalogSpecies { Id = 1, DisplayName = "Known", DescriptionRu = "Первый", Types = ["grass"] },
                new CatalogSpecies { Id = 2, DisplayName = "Secret", DescriptionRu = "Второй", Types = ["fire"] }]);
        Assert.Equal(1, vm.GetEntry(individual.Id, DateTimeOffset.UtcNow)!.DisplaySpeciesId);
        Assert.Equal(individual.Id, vm.GetSpeciesEntries().Single(e => e.SpeciesId == 2).OwnedIndividuals.Single().IndividualId);
    }

    [Fact]
    public void SpeciesSortPutsClosedEntriesAfterOpenTypesWithoutDisclosingType()
    {
        var entries = CreateVm().GetSpeciesEntries();
        var sorted = CollectionViewModel.FilterSpecies(entries, "", null, null, false, SpeciesSortOrder.Type);
        Assert.Equal(new[] { 1, 2, 3 }, sorted.Select(e => e.SpeciesId));
        Assert.DoesNotContain(sorted, e => e.SpeciesId == 2 && e.Types.Count > 0);
    }

    [Fact]
    public void OwnedBusyStateReflectsLiveProviderWithoutChangingItsGuid()
    {
        var collection = new PokemonCollection();
        var individual = new PokemonIndividual { SpeciesId = 1, EvolutionChainId = 1, ChainLength = 1,
            Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow };
        collection.Add(individual);
        var busyIds = new HashSet<Guid>();
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(),
            [], [], busyIndividualIds: () => busyIds, species: [
                new CatalogSpecies { Id = 1, DisplayName = "Known", DescriptionRu = "Факт", Types = ["grass"] }]);

        Assert.False(vm.GetSpeciesEntries().Single().OwnedIndividuals.Single().IsBusy);
        busyIds.Add(individual.Id);
        var busy = vm.GetSpeciesEntries().Single().OwnedIndividuals.Single();
        Assert.Equal(individual.Id, busy.IndividualId);
        Assert.True(busy.IsBusy);
    }

    [Fact]
    public void OwnedCardRefreshesWhenOnlyBusyStateOrLevelChanges() => WpfTestHost.Run(() =>
    {
        var state = new AppState();
        var individual = new PokemonIndividual { SpeciesId = 1, EvolutionChainId = 1, ChainLength = 1,
            Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow };
        state.Collection.Add(individual);
        var busy = new HashSet<Guid>();
        var level = 2;
        var vm = new CollectionViewModel(state.Collection, state.CareStateByIndividualId, new CareModeService(),
            [], [], busyIndividualIds: () => busy, levelFor: _ => level);
        var view = new CollectionView(vm, state,
            new SpriteCache(Path.Combine(Path.GetTempPath(), "pokedex-test", Guid.NewGuid().ToString("N"))),
            () => { }, () => { }, () => { }, () => null, () => false, () => false);
        ((TabControl)((Grid)view.Content).Children[0]).SelectedIndex = 1;
        WpfTestHost.Layout(view);
        var grid = (ItemsControl)view.FindName("PokedexGrid");
        var first = Assert.IsType<PokedexCard>(Assert.Single(grid.Items));
        Assert.Equal("Ур. 2", first.LevelText);
        Assert.Equal("На столе", first.AvailabilityText);

        busy.Add(individual.Id);
        level = 3;
        view.Refresh();
        var refreshed = Assert.IsType<PokedexCard>(Assert.Single(grid.Items));
        Assert.Equal("Ур. 3", refreshed.LevelText);
        Assert.Equal("Занят", refreshed.AvailabilityText);
    });

    [Fact]
    public async Task DelayedFavoriteCommitDoesNotRepaintAnotherOpenedDetail()
    {
        var state = new AppState();
        var first = new PokemonIndividual { SpeciesId = 1, EvolutionChainId = 1, ChainLength = 1,
            Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow };
        var second = new PokemonIndividual { SpeciesId = 2, EvolutionChainId = 2, ChainLength = 1,
            Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow };
        state.Collection.Add(first);
        state.Collection.Add(second);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CollectionView? view = null;
        WpfTestHost.Run(() =>
        {
            view = new CollectionView(CreateVm(state.Collection), state,
                new SpriteCache(Path.Combine(Path.GetTempPath(), "pokedex-test", Guid.NewGuid().ToString("N"))),
                () => { }, () => { }, () => { }, () => null, () => false, () => false,
                commitState: _ => gate.Task);
            Assert.True(view.OpenIndividual(first.Id));
            ((Button)view.FindName("DetailFavoriteButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.True(view.OpenIndividual(second.Id));
            first.IsFavorite = true;
        });
        gate.SetResult();
        await view!.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle).Task;
        WpfTestHost.Run(() =>
        {
            Assert.Equal(second.Id, view!.SelectedIndividualId);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)view.FindName("DetailFavoriteBadge")).Visibility);
            Assert.Equal(string.Empty, ((TextBlock)view.FindName("DetailErrorText")).Text);
        });
    }

    [Fact]
    public async Task DelayedCommitErrorStaysOffAnotherDetailAndSelectKeepsItsOriginalGuid()
    {
        var state = new AppState();
        var first = new PokemonIndividual { SpeciesId = 1, EvolutionChainId = 1, ChainLength = 1,
            Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow };
        var second = new PokemonIndividual { SpeciesId = 2, EvolutionChainId = 2, ChainLength = 1,
            Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow };
        state.Collection.Add(first);
        state.Collection.Add(second);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Guid? requestedActiveId = null;
        CollectionView? view = null;
        WpfTestHost.Run(() =>
        {
            view = new CollectionView(CreateVm(state.Collection), state,
                new SpriteCache(Path.Combine(Path.GetTempPath(), "pokedex-test", Guid.NewGuid().ToString("N"))),
                () => { }, () => { }, () => { }, () => null, () => false, () => false,
                commitState: update =>
                {
                    var next = state.ToSaveData(0);
                    update(next);
                    requestedActiveId = next.ActiveIndividualId;
                    return gate.Task;
                });
            Assert.True(view.OpenIndividual(second.Id));
            ((Button)view.FindName("DetailSelectButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.True(view.OpenIndividual(first.Id));
        });
        Assert.Equal(second.Id, requestedActiveId);
        gate.SetException(new IOException("Отложенный отказ записи"));
        await view!.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle).Task;
        WpfTestHost.Run(() =>
        {
            Assert.Equal(first.Id, view.SelectedIndividualId);
            Assert.Equal(string.Empty, ((TextBlock)view.FindName("DetailErrorText")).Text);
            Assert.True(((Button)view.FindName("DetailSelectButton")).IsEnabled == false);
        });
    }

    [Fact]
    public async Task FavoriteCompletionToleratesItsIndividualBeingReleased()
    {
        var state = new AppState();
        var individual = new PokemonIndividual { SpeciesId = 1, EvolutionChainId = 1, ChainLength = 1,
            Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow };
        state.Collection.Add(individual);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CollectionView? view = null;
        WpfTestHost.Run(() =>
        {
            view = new CollectionView(CreateVm(state.Collection), state,
                new SpriteCache(Path.Combine(Path.GetTempPath(), "pokedex-test", Guid.NewGuid().ToString("N"))),
                () => { }, () => { }, () => { }, () => null, () => false, () => false,
                commitState: _ => gate.Task);
            Assert.True(view.OpenIndividual(individual.Id));
            ((Button)view.FindName("DetailFavoriteButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            state.Collection.Release(individual.Id);
        });
        gate.SetResult();
        await view!.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle).Task;
        WpfTestHost.Run(() => Assert.Null(view.SelectedIndividualId));
    }

    [Fact]
    public void NewSpeciesScreenRendersClosedSearchAndExplicitOwnedChoicesAtCompactSize() => WpfTestHost.Run(() =>
    {
        var state = new AppState();
        var first = new PokemonIndividual { SpeciesId = 1, EvolutionChainId = 1, ChainLength = 1, Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow };
        var second = new PokemonIndividual { SpeciesId = 1, EvolutionChainId = 1, ChainLength = 1, Nature = Nature.Hardy, IsShiny = true, FirstTraitId = "gatherer", CaughtAtUtc = DateTimeOffset.UtcNow };
        state.Collection.Add(first);
        state.Collection.Add(second);
        var view = new CollectionView(CreateVm(state.Collection), state,
            new SpriteCache(Path.Combine(Path.GetTempPath(), "pokedex-test", Guid.NewGuid().ToString("N"))),
            () => { }, () => { }, () => { }, () => null, () => false, () => false,
            getSpeciesAssetPath: (_, _) => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".gif"));
        ((TabControl)((Grid)view.Content).Children[0]).SelectedIndex = 1;
        ((Button)view.FindName("SpeciesModeButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        WpfTestHost.Layout(view, 420, 420);
        var grid = (ListBox)view.FindName("SpeciesGrid");
        Assert.Equal(3, grid.Items.Count);
        grid.SelectedIndex = 0;
        WpfTestHost.Layout(view, 420, 420);
        Assert.Equal(2, ((ItemsControl)view.FindName("SpeciesOwnedList")).Items.Count); // both forms on one page
        Assert.True(view.OpenIndividual(second.Id));
        Assert.Equal(second.Id, view.SelectedIndividualId);
        WpfTestHost.Layout(view, 320, 420);
        Assert.True(((Border)view.FindName("DetailCard")).ActualWidth <= 320);
        Assert.Contains("Собиратель", ((TextBlock)view.FindName("DetailTraitsText")).Text);
        Assert.Equal(CollectionPresentation.Fact(1), ((TextBlock)view.FindName("DetailDescriptionText")).Text);
        Assert.False(view.OpenIndividual(Guid.NewGuid()));

        var card = Assert.IsType<Border>(((ItemsControl)view.FindName("PokedexGrid")).ItemTemplate.LoadContent());
        card.DataContext = Assert.IsType<PokedexCard>(((ItemsControl)view.FindName("PokedexGrid")).Items[0]);
        using var inputSource = new HwndSource(new HwndSourceParameters("pokedex-keyboard-test") { WindowStyle = 0 });
        card.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, 0, Key.Enter)
            { RoutedEvent = Keyboard.KeyDownEvent });
        Assert.Equal(first.Id, view.SelectedIndividualId);
        view.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, 0, Key.Escape)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Assert.Null(view.SelectedIndividualId);
        card.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, 0, Key.Space)
            { RoutedEvent = Keyboard.KeyDownEvent });
        Assert.Equal(first.Id, view.SelectedIndividualId);

        var search = (TextBox)view.FindName("SpeciesSearchBox");
        search.Text = "Secret";
        Assert.Empty(grid.Items);
        search.Text = "#0002";
        Assert.Single(grid.Items);
        grid.SelectedIndex = 0;
        Assert.DoesNotContain("Secret", ((TextBlock)view.FindName("SpeciesDetailName")).Text);
        Assert.DoesNotContain("Секрет", ((TextBlock)view.FindName("SpeciesDetailDescription")).Text);
    });

    [Fact]
    public void FailedCommitKeepsTheIndividualAndOpenDetailUnchanged() => WpfTestHost.Run(() =>
    {
        var state = new AppState();
        var individual = new PokemonIndividual { SpeciesId = 1, EvolutionChainId = 1, ChainLength = 1,
            Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow };
        state.Collection.Add(individual);
        var view = new CollectionView(CreateVm(state.Collection), state,
            new SpriteCache(Path.Combine(Path.GetTempPath(), "pokedex-test", Guid.NewGuid().ToString("N"))),
            () => { }, () => { }, () => { }, () => null, () => false, () => false,
            commitState: _ => Task.FromException(new IOException("Не удалось сохранить")));
        Assert.True(view.OpenIndividual(individual.Id));
        ((Button)view.FindName("DetailFavoriteButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.False(individual.IsFavorite);
        Assert.Equal(individual.Id, view.SelectedIndividualId);
        Assert.Contains("Не удалось сохранить", ((TextBlock)view.FindName("DetailErrorText")).Text);
    });

    private static CollectionViewModel CreateVm(PokemonCollection? collection = null) => new(
        collection ?? new PokemonCollection(), new Dictionary<Guid, CareState>(), new CareModeService(),
        [], [], species: [
            new CatalogSpecies { Id = 1, DisplayName = "Known", DescriptionRu = "Факт", Types = ["grass"], NormalAvailable = true },
            new CatalogSpecies { Id = 2, DisplayName = "Secret", DescriptionRu = "Секрет", Types = ["fire"], Rarity = "Rare", NormalAvailable = true },
            new CatalogSpecies { Id = 3, DisplayName = "Unavailable", DescriptionRu = "Нет", Types = ["water"], NormalAvailable = false }
        ], discoveredSpeciesIds: () => new HashSet<int> { 1 }, routeHints: new Dictionary<int, string> { [2] = "Лесная роща" });
}
