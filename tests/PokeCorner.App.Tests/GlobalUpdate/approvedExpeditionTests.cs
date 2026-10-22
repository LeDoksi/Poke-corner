using System.Windows;
using System.Windows.Controls;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;
namespace PokeCorner.App.Tests.GlobalUpdate;
public class ApprovedExpeditionTests
{
    internal static readonly Lazy<CatalogPackage> Catalog = new(() => CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data")));
    internal static SaveData State(int count = 4)
    {
        var s = Catalog.Value.Species.First(s => Catalog.Value.CanIssueDirectly(s.Id, false));
        return new SaveData { SchemaVersion = 2, CatalogRevision = Catalog.Value.CatalogRevision, BalanceRevision = "test", MigrationId = SaveMigration.MigrationId, Settings = new(),
            Berries = SaveMigration.BerryIds.ToDictionary(x => x, _ => 0),
            Items = SaveMigration.ItemIds.ToDictionary(x => x, _ => 0),
            BerryRemainders = SaveMigration.BerryIds.ToDictionary(x => x, _ => new RationalRemainder()),
            Individuals = Enumerable.Range(0, count).Select(i => new PokemonIndividual { SpeciesId = s.Id,
                CurrentSpeciesId = s.Id, FamilyRootId = s.Id, ReachedSpeciesIds = [s.Id], EvolutionChainId = s.EvolutionChainId,
                ChainLength = 3, Nature = Nature.Bold, IsShiny = i % 2 == 1, CaughtAtUtc = DateTimeOffset.UtcNow,
                FirstTraitId = "gatherer", Nickname = "Питомец " + i }).ToList() };
    }
    internal static ExpeditionView View(SaveData state) => new(() => state,
        r => new ExpeditionService(Catalog.Value, state).Preview(r),
        r => Task.FromResult(new ExpeditionService(Catalog.Value, state).Start(r)),
        (id, now) => Task.FromResult(new ExpeditionService(Catalog.Value, state).Cancel(id, now)),
        (id, op) => Task.FromResult(new ExpeditionService(Catalog.Value, state).Claim(id, op)),
        _ => Task.FromResult(default(EncounterOpenResult)!), Catalog.Value);
    internal static T Find<T>(ExpeditionView v, string name) where T : class => Assert.IsType<T>(v.FindName(name));
    internal static void Click(ExpeditionView v, string name) => Find<Button>(v, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    internal static Button Candidate(ExpeditionView v, Guid id) => Find<WrapPanel>(v, "PickerMembers").Children.OfType<Button>().Single(b => Equals(b.Tag, id));
    [Theory][InlineData(1000, 650)][InlineData(840, 540)]
    public void EmptyTeamDraftCancelAndThreeExactIndividuals(int width, int height) => WpfTestHost.Run(() =>
    {
        var data = State(); foreach (var pet in data.Individuals) pet.Nickname = "Оченьдлиннаякириллическаякличка";
        var v = View(data); WpfTestHost.Layout(v, width, height);
        Assert.False(Find<Button>(v, "StartButton").IsEnabled);
        Assert.Equal(3, Find<System.Windows.Controls.Primitives.UniformGrid>(v, "TeamSlots").Children.Count);
        Click(v, "ChooseTeamButton");
        WpfTestHost.Layout(v, width, height);
        var done = Find<Button>(v, "PickerDoneButton");
        var doneBounds = done.TransformToAncestor(v).TransformBounds(new Rect(done.RenderSize));
        Assert.True(doneBounds.Bottom <= height && doneBounds.Right <= width && doneBounds.Top >= 0);
        Assert.True(Find<ScrollViewer>(v, "PickerScroll").ViewportHeight > 0);
        Candidate(v, data.Individuals[0].Id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Click(v, "PickerCancelButton"); Assert.False(Find<Button>(v, "StartButton").IsEnabled);
        Click(v, "ChooseTeamButton");
        foreach (var pet in data.Individuals.Take(3)) Candidate(v, pet.Id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(Candidate(v, data.Individuals[3].Id).IsEnabled);
        Assert.True(Find<Button>(v, "PickerDoneButton").IsEnabled);
        Click(v, "PickerDoneButton"); Assert.True(Find<Button>(v, "StartButton").IsEnabled);
        Assert.DoesNotContain("%", Find<TextBlock>(v, "PreviewText").Text + Find<TextBlock>(v, "ContributionsText").Text);
        Click(v, "ChooseTeamButton"); Click(v, "PickerResetButton");
        Assert.Contains("3 / 3", Find<TextBlock>(v, "PickerCountText").Text);
        Click(v, "PickerCancelButton"); Click(v, "StartButton");
        Assert.Equal(data.Individuals.Take(3).Select(x => x.Id), data.Expedition!.ParticipantIds);
    });
    [Fact]
    public void EightHundredIndividualsArePagedSearchableAndBusyReasonsRemainVisible() => WpfTestHost.Run(() =>
    {
        var data = State(800); data.ActiveIndividualId = data.Individuals[0].Id;
        data.Nest = new NurseryState { Status = "running", ParticipantIds = [data.Individuals[1].Id], Child = data.Individuals[1] };
        var v = View(data); Click(v, "ChooseTeamButton");
        Assert.Equal(12, Find<WrapPanel>(v, "PickerMembers").Children.Count);
        Find<TextBox>(v, "PickerSearchBox").Text = "Питомец 799";
        Assert.Equal(data.Individuals[799].Id, Find<WrapPanel>(v, "PickerMembers").Children.OfType<Button>().Single().Tag);
        Click(v, "PickerResetButton"); Find<CheckBox>(v, "PickerAvailableBox").IsChecked = false;
        Find<ComboBox>(v, "PickerSortBox").SelectedIndex = 2;
        Assert.False(Candidate(v, data.Individuals[0].Id).IsEnabled);
        Assert.False(Candidate(v, data.Individuals[1].Id).IsEnabled);
        Assert.Contains("рабочем столе", Candidate(v, data.Individuals[0].Id).ToolTip.ToString());
        Assert.Contains("питомнике", Candidate(v, data.Individuals[1].Id).ToolTip.ToString());
    });
    [Fact]
    public void ReadyMarkerUsesCurrentSnapshotAndSurvivesNavigationUntilClaim() => WpfTestHost.Run(() =>
    {
        var data = State();
        new ExpeditionService(Catalog.Value, data).Start(new("grove", "easy", 30, [data.Individuals[0].Id], Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-2)));
        var menu = ShellTests.CreateMenu(new PokeCorner.Core.AppState(), () => data);
        try {
            Assert.Equal(Visibility.Visible, Assert.IsType<Border>(menu.FindName("ExpeditionReadyBadge")).Visibility);
            menu.Navigate(MenuSection.Expeditions); menu.Navigate(MenuSection.Pokedex);
            Assert.Equal(Visibility.Visible, Assert.IsType<Border>(menu.FindName("ExpeditionReadyBadge")).Visibility);
            new ExpeditionService(Catalog.Value, data).Claim(data.Expedition!.Id, Guid.NewGuid());
            menu.Navigate(MenuSection.Corner);
            Assert.Equal(Visibility.Collapsed, Assert.IsType<Border>(menu.FindName("ExpeditionReadyBadge")).Visibility);
        } finally { menu.Close(); }
    });
    [Fact]
    public void FiltersUseEitherUnlockedTraitAndCurrentRouteDifficultyRecommendations() => WpfTestHost.Run(() =>
    {
        var data = State(4); var levels = new LevelProgression(Catalog.Value.DataRoot);
        data.Individuals[0].TotalXp = levels.XpForLevel(9); data.Individuals[0].SecondTraitId = "mentor";
        data.Individuals[1].TotalXp = levels.XpForLevel(10); data.Individuals[1].SecondTraitId = "mentor";
        data.Individuals[2].TotalXp = levels.XpForLevel(25); data.Individuals[2].SecondTraitId = "tracker";
        var v = View(data); Click(v, "ChooseTeamButton");
        var traitBox = Find<ComboBox>(v, "PickerTraitBox");
        traitBox.SelectedItem = traitBox.Items.OfType<ComboBoxItem>().Single(i => Equals(i.Tag, "mentor"));
        Assert.Equal(data.Individuals[1].Id, Find<WrapPanel>(v, "PickerMembers").Children.OfType<Button>().Single().Tag);
        Click(v, "PickerResetButton");
        Assert.Equal(data.Individuals[2].Id, Find<WrapPanel>(v, "PickerMembers").Children.OfType<Button>().First().Tag);
        Click(v, "PickerCancelButton");
        v.RouteButtons.Children.OfType<Button>().Single(b => Equals(b.Tag, "volcano")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Find<ListBox>(v, "DifficultyBox").SelectedIndex = 2;
        Click(v, "ChooseTeamButton");
        Assert.Contains("25", Find<TextBlock>(v, "PickerRouteHintText").Text);
        Assert.Contains("Огненный", Find<TextBlock>(v, "PickerRouteHintText").Text);
        Find<CheckBox>(v, "PickerRouteBox").IsChecked = true;
        Assert.Empty(Find<WrapPanel>(v, "PickerMembers").Children.OfType<Button>());
        Click(v, "PickerResetButton"); Find<ComboBox>(v, "PickerSortBox").SelectedIndex = 1;
        Assert.Equal(data.Individuals[2].Id, Find<WrapPanel>(v, "PickerMembers").Children.OfType<Button>().First().Tag);
    });
    [Fact]
    public void ConfirmationRechecksClockAndCannotCancelAfterUnload() => WpfTestHost.Run(() =>
    {
        var data = State(); var service = new ExpeditionService(Catalog.Value, data);
        service.Start(new("grove", "easy", 30, [data.Individuals[0].Id], Guid.NewGuid(), DateTimeOffset.UtcNow));
        var confirmation = new TaskCompletionSource<bool>(); var calls = 0;
        var v = new ExpeditionView(() => data, r => service.Preview(r), r => Task.FromResult(service.Start(r)),
            (id, now) => { calls++; return Task.FromResult(service.Cancel(id, now)); },
            (id, op) => Task.FromResult(service.Claim(id, op)), _ => Task.FromResult(default(EncounterOpenResult)!), Catalog.Value,
            confirmAsync: (_, _, _) => confirmation.Task);
        Click(v, "CancelButton"); Click(v, "CancelButton"); Assert.Equal(0, calls);
        data.Expedition!.ReadyAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        confirmation.SetResult(true); Pump(); Assert.Equal(0, calls);
        Assert.Equal(Visibility.Visible, Find<Button>(v, "ClaimButton").Visibility);
        data.Expedition.ReadyAtUtc = DateTimeOffset.UtcNow.AddHours(1); v.Refresh(); confirmation = new();
        Click(v, "CancelButton"); v.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        confirmation.SetResult(true); Pump(); Assert.Equal(0, calls); Assert.Equal("running", data.Expedition.Status);
        var timer = (System.Windows.Threading.DispatcherTimer)typeof(ExpeditionView).GetField("_timer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(v)!;
        Assert.False(timer.IsEnabled); v.Refresh(); Assert.False(timer.IsEnabled);
    });
    internal static void Pump()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
    [Fact]
    public async Task ReadyToastObservesOnlyCommittedReadinessOncePerTripAcrossMenus()
    {
        var data = State(); var now = DateTimeOffset.UtcNow;
        new ExpeditionService(Catalog.Value, data).Start(new("grove", "easy", 30, [data.Individuals[0].Id], Guid.NewGuid(), now.AddHours(-2)));
        var live = new PokeCorner.Core.AppState(); live.Hydrate(data);
        var type = typeof(ExpeditionView).Assembly.GetType("PokeCorner.App.ExpeditionReadyNotifications")!;
        var boundary = Activator.CreateInstance(type, nonPublic: true)!;
        var observe = type.GetMethod("ObserveCommitted", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var messages = new List<string>();
        void Tick() => observe.Invoke(boundary, [live.ToSaveData(0), (Action<string, string>)((title, body) => messages.Add(body))]);
        Tick(); Tick(); Assert.Empty(messages); // Due clock alone must never announce a refused candidate.
        var root = Path.Combine(Path.GetTempPath(), "pokecorner-ready-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try {
            var failed = new BackgroundSaveWriter(new JsonSaveStore(Path.Combine(root, "failed.json")), _ => throw new IOException("refused progress"));
            await Assert.ThrowsAsync<IOException>(() => live.CommitAsync(failed, next => new ExpeditionService(Catalog.Value, next).RefreshReady(now), () => 0));
            Tick(); Assert.Empty(messages); Assert.Equal("running", live.ToSaveData(0).Expedition!.Status);
            var writer = new BackgroundSaveWriter(new JsonSaveStore(Path.Combine(root, "saved.json")));
            await live.CommitAsync(writer, next => new ExpeditionService(Catalog.Value, next).RefreshReady(now), () => 0);
            Tick(); Tick(); Assert.Single(messages); // Menu absent.
            WpfTestHost.Run(() => {
                var menu = ShellTests.CreateMenu(new PokeCorner.Core.AppState(), () => live.ToSaveData(0));
                try { menu.Navigate(MenuSection.Expeditions); Tick(); menu.Navigate(MenuSection.Pokedex); Tick(); }
                finally { menu.Close(); }
                var reloaded = View(live.ToSaveData(0)); reloaded.Refresh(); Tick();
            });
            Assert.Single(messages);
            var restartedBoundary = Activator.CreateInstance(type, nonPublic: true)!;
            observe.Invoke(restartedBoundary, [live.ToSaveData(0), (Action<string, string>)((_, body) => messages.Add(body))]);
            Assert.Equal(2, messages.Count); // One helpful reminder in a new app session.
            await live.CommitAsync(writer, next => new ExpeditionService(Catalog.Value, next).Claim(data.Expedition!.Id, Guid.NewGuid()), () => 0);
            Tick(); Assert.Equal(2, messages.Count);
            await live.CommitAsync(writer, next => new ExpeditionService(Catalog.Value, next).Start(new("lake", "easy", 30,
                [data.Individuals[0].Id], Guid.NewGuid(), now.AddHours(-2))), () => 0);
            Tick(); Assert.Equal(2, messages.Count);
            await live.CommitAsync(writer, next => new ExpeditionService(Catalog.Value, next).RefreshReady(now), () => 0);
            Tick(); Tick(); Assert.Equal(3, messages.Count);
        } finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void CurrentRouteRanksMatchingTypeBeforeLevelAndShowsSeparateResult() => WpfTestHost.Run(() =>
    {
        var catalog = Catalog.Value; var data = State(0); var levels = new LevelProgression(catalog.DataRoot);
        PokemonIndividual Pet(string type, int level) {
            var species = catalog.Species.First(s => s.Types.Contains(type) && catalog.CanIssueDirectly(s.Id, false));
            return new PokemonIndividual { SpeciesId = species.Id, CurrentSpeciesId = species.Id, FamilyRootId = species.Id,
                EvolutionChainId = species.EvolutionChainId, ChainLength = 3, Nature = Nature.Bold, IsShiny = false,
                CaughtAtUtc = DateTimeOffset.UtcNow, ReachedSpeciesIds = [species.Id], FirstTraitId = "gatherer", TotalXp = levels.XpForLevel(level) };
        }
        var grass = Pet("grass", 1); var fire = Pet("fire", 25); data.Individuals.AddRange([grass, fire]);
        var v = View(data); Click(v, "ChooseTeamButton");
        Assert.Equal(grass.Id, Find<WrapPanel>(v, "PickerMembers").Children.OfType<Button>().First().Tag);
        Click(v, "PickerCancelButton");
        v.RouteButtons.Children.OfType<Button>().Single(b => Equals(b.Tag, "volcano")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.NotNull(Find<Image>(v, "LandscapeImage").Source);
        Click(v, "ChooseTeamButton");
        Assert.Equal(fire.Id, Find<WrapPanel>(v, "PickerMembers").Children.OfType<Button>().First().Tag);
        var typeBox = Find<ComboBox>(v, "PickerTypeBox");
        typeBox.SelectedItem = typeBox.Items.OfType<ComboBoxItem>().Single(i => Equals(i.Tag, "grass"));
        Assert.Equal(grass.Id, Find<WrapPanel>(v, "PickerMembers").Children.OfType<Button>().Single().Tag);
        Candidate(v, grass.Id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Click(v, "PickerDoneButton");
        var rewardCards = Find<System.Windows.Controls.Primitives.UniformGrid>(v, "PreviewRewardCards");
        Assert.Equal(3, rewardCards.Children.Count);
        var request = new ExpeditionRequest("volcano", "easy", 30, [grass.Id], Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-2));
        var service = new ExpeditionService(catalog, data); service.Start(request); data.Expedition!.Status = "ready";
        service.Claim(request.OperationId, Guid.NewGuid()); v.Refresh();
        // A one-member trip reveals no extras or encounter, so there is nothing new to show: straight back to preparation.
        Assert.Equal(Visibility.Visible, Find<StackPanel>(v, "PreparationPanel").Visibility);
        Assert.Equal(Visibility.Collapsed, Find<StackPanel>(v, "ResultPanel").Visibility);
    });

    [Fact]
    public async Task ActualCancellationUsesDangerConfirmationAndDecliningKeepsTrip()
    {
        var data = State(); var request = new ExpeditionRequest("grove", "easy", 30, [data.Individuals[0].Id], Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.True(new ExpeditionService(Catalog.Value, data).Start(request).Success);
        ExpeditionView view = null!;
        WpfTestHost.Run(() =>
        {
            view = View(data); WpfTestHost.Layout(view, 840, 540); Click(view, "CancelButton");
            var overlay = Find<ConfirmationOverlay>(view, "LocalConfirmation");
            Assert.Equal(Visibility.Visible, overlay.Visibility);
            Assert.Same(overlay.FindResource("UiDangerButtonStyle"), ((Button)overlay.FindName("ConfirmButton")).Style);
            ((Button)overlay.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });
        await view.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        WpfTestHost.Run(() =>
        {
            Assert.Equal("running", data.Expedition!.Status); Assert.Equal(request.OperationId, data.Expedition.Id);
            Assert.Equal(Visibility.Collapsed, ((ConfirmationOverlay)view.FindName("LocalConfirmation")).Visibility);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

}
