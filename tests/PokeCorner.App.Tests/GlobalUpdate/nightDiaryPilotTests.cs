using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Tests.GlobalUpdate;

public class NightDiaryPilotTests
{
    private static readonly Lazy<CatalogPackage> Catalog = new(() => CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data")));
    private static SaveData State()
    {
        var data = ApprovedExpeditionTests.State(0);
        foreach (var id in new[] { 1, 152, 25, 133, 1, 12 })
        {
            var s = Catalog.Value.Species.Single(x => x.Id == id);
            data.Individuals.Add(new PokemonIndividual {
                SpeciesId = id, CurrentSpeciesId = id, FamilyRootId = s.FamilyRootId,
                EvolutionChainId = s.EvolutionChainId, ChainLength = 3, ReachedSpeciesIds = [id],
                IsShiny = false, TotalXp = 1200, FirstTraitId = "caring-parent", Nature = Nature.Bold,
                CaughtAtUtc = DateTimeOffset.Parse("2026-10-20T12:00:00Z")
            });
        }
        data.ActiveIndividualId = data.Individuals[3].Id;
        data.ShopCoins = 12500;
        return data;
    }

    private static ExpeditionView View(SaveData data, Func<ExpeditionRequest, Task<ExpeditionAction>>? start = null) =>
        new(() => data, r => new ExpeditionService(Catalog.Value, data).Preview(r),
            start ?? (r => Task.FromResult(new ExpeditionService(Catalog.Value, data).Start(r))),
            (id, now) => Task.FromResult(new ExpeditionService(Catalog.Value, data).Cancel(id, now)),
            (id, op) => Task.FromResult(new ExpeditionService(Catalog.Value, data).Claim(id, op)),
            id => Task.FromResult(new EncounterService(Catalog.Value).Open(data, id)), Catalog.Value);
    private static T Find<T>(FrameworkElement view, string name) where T : class => Assert.IsAssignableFrom<T>(view.FindName(name));
    private static void Click(FrameworkElement view, string name) => Find<Button>(view, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Select(ExpeditionView view, SaveData data, int count = 3)
    {
        Click(view, "ChooseTeamButton");
        foreach (var pet in data.Individuals.Take(count))
            Find<WrapPanel>(view, "PickerMembers").Children.OfType<Button>().Single(b => Equals(b.Tag, pet.Id))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Click(view, "PickerDoneButton");
    }
    private static AppMenuWindow Menu(ExpeditionView view, SaveData data)
    {
        var state = new AppState(); state.Hydrate(data);
        var menu = ShellTests.CreateMenu(state, () => data);
        Find<TabItem>(menu, "ExpeditionsTab").Content = view;
        menu.Navigate(MenuSection.Expeditions);
        return menu;
    }

    [Theory]
    [InlineData(1, "1 монета"), InlineData(4, "4 монеты"), InlineData(12, "12 монет"), InlineData(21, "21 монета"), InlineData(113, "113 монет"), InlineData(122, "122 монеты")]
    public void CoinCountsUseRussianPlural(long count, string expected) => Assert.Equal(expected, ExpeditionView.Coins(count));

    [Fact]
    public void ApprovedThreeSpeciesFixtureUsesActualPreviewAndDraftCancel() => WpfTestHost.Run(() =>
    {
        var data = State();
        var request = new ExpeditionRequest("grove", "normal", 90, data.Individuals.Take(3).Select(p => p.Id).ToArray(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var preview = new ExpeditionService(Catalog.Value, data).Preview(request);
        Assert.Equal("rawst", preview.PrimaryBerryId); Assert.Equal(3, preview.PrimaryBerries);
        Assert.Equal(12, preview.Coins); Assert.Equal(150, preview.XpPerParticipant);
        var view = View(data);
        Click(view, "ChooseTeamButton");
        Assert.False(Find<Button>(view, "PickerDoneButton").IsEnabled);
        var candidate = Find<WrapPanel>(view, "PickerMembers").Children.OfType<Button>().Single(b => Equals(b.Tag, data.Individuals[0].Id));
        candidate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(Find<Button>(view, "PickerDoneButton").IsEnabled);
        Click(view, "PickerCancelButton");
        Assert.False(Find<Button>(view, "StartButton").IsEnabled);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    });

    [Fact]
    public async Task FailedStartRetainsOperationAndDoesNotPublishTentativeTrip()
    {
        var data = State(); var pending = new TaskCompletionSource<ExpeditionAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = new List<ExpeditionRequest>(); ExpeditionView view = null!;
        WpfTestHost.Run(() => {
            view = View(data, r => { requests.Add(r); return pending.Task; }); Select(view, data);
            Click(view, "StartButton"); Click(view, "StartButton");
            Assert.Single(requests); Assert.Null(data.Expedition); Assert.False(Find<Button>(view, "StartButton").IsEnabled);
        });
        pending.SetException(new IOException("Synthetic write refusal"));
        await view.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        WpfTestHost.Run(() => {
            Assert.Null(data.Expedition); Assert.True(Find<Button>(view, "StartButton").IsEnabled);
            Click(view, "StartButton"); Assert.Equal(2, requests.Count);
            Assert.Equal(requests[0].OperationId, requests[1].OperationId);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void CaptureIsolatedNightDiaryMatrix() => WpfTestHost.Run(() =>
    {
        var output = Environment.GetEnvironmentVariable("POKE_NIGHT_DIARY_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        Directory.CreateDirectory(output);
        void Capture(string name, ExpeditionView view, SaveData data)
        {
            var menu = Menu(view, data);
            try {
                menu.ShowActivated = false; menu.ShowInTaskbar = false; menu.Left = -10000; menu.Top = -10000; menu.Show();
                var root = (FrameworkElement)menu.Content;
                foreach (var size in new[] { (1000, 760), (840, 650) }) {
                    menu.Width = size.Item1; menu.Height = size.Item2;
                    WpfTestHost.Layout(menu, size.Item1, size.Item2);
                    menu.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                    WpfTestHost.Layout(root, size.Item1, size.Item2);
                    Assert.Equal(size.Item1 == 840 ? 112 : 136, ((GridLength)menu.Resources["NdRailWidth"]).Value);
                    var action = name.StartsWith("picker") ? Find<Button>(view, "PickerDoneButton") :
                        name == "ready" ? Find<Button>(view, "ClaimButton") :
                        name == "result" ? Find<Button>(view, "ContinueButton") :
                        name is "preparation" or "empty" or "error" ? Find<Button>(view, "StartButton") :
                        name == "running" ? Find<Button>(view, "CancelButton") : null;
                    if (action is not null) {
                        Assert.True(action.IsVisible && action.ActualWidth > 40 && action.ActualHeight > 20, name + " primary action not visible/measured");
                        var bounds = action.TransformToAncestor(root).TransformBounds(new Rect(action.RenderSize));
                        Assert.True(bounds.Top >= 0 && bounds.Bottom <= size.Item2 && bounds.Right <= size.Item1, name + " action clipped: " + bounds);
                    }
                    foreach (var scale in new[] { 1d, 1.5d, 2d }) { var pixelWidth = (int)(size.Item1 * scale); var pixelHeight = (int)(size.Item2 * scale); var bmp = new RenderTargetBitmap(pixelWidth, pixelHeight, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                    bmp.Render(root); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bmp));
                    using var stream = File.Create(Path.Combine(output, name + "-" + size.Item1 + "x" + size.Item2 + (scale == 1 ? "" : "-" + (int)(scale * 100) + "pct") + ".png"));
                    png.Save(stream); Assert.True(stream.Length > 1000); Assert.Equal(pixelWidth, bmp.PixelWidth); Assert.Equal(pixelHeight, bmp.PixelHeight); }
                }
            } finally { menu.Close(); view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
        }
        var empty = State(); Capture("empty", View(empty), empty);
        var selected = State(); var prepared = View(selected); Select(prepared, selected); Capture("preparation", prepared, selected);
        var pickerData = State(); var picker = View(pickerData); Click(picker, "ChooseTeamButton");
        foreach (var pet in pickerData.Individuals.Take(2))
            Find<WrapPanel>(picker, "PickerMembers").Children.OfType<Button>().Single(b => Equals(b.Tag, pet.Id)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Find<CheckBox>(picker, "PickerAvailableBox").IsChecked = false; Capture("picker", picker, pickerData);
        var searchData = State(); var search = View(searchData); Click(search, "ChooseTeamButton");
        Find<TextBox>(search, "PickerSearchBox").Text = "not-a-real-pokemon"; Capture("picker-empty", search, searchData);
        var running = State(); var trip = new ExpeditionRequest("grove", "normal", 90, running.Individuals.Take(3).Select(x => x.Id).ToArray(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-30));
        new ExpeditionService(Catalog.Value, running).Start(trip); Capture("running", View(running), running);
        var cancelView = View(running); Click(cancelView, "CancelButton"); Capture("cancel", cancelView, running);
        running.Expedition!.ReadyAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1); running.Expedition.Status = "ready";
        Capture("ready", View(running), running);
        new ExpeditionService(Catalog.Value, running).Claim(trip.OperationId, Guid.NewGuid());
        Capture("claimed-plain", View(running), running);
        running.Expedition!.Rewards.ExtraBerries["oran"] = 2; // the result screen only appears when the claim revealed extras
        Capture("result", View(running), running);
        var failed = State(); var error = View(failed, _ => Task.FromException<ExpeditionAction>(new IOException("Synthetic write refusal — no profile used")));
        Select(error, failed); Click(error, "StartButton"); Capture("error", error, failed);
        File.WriteAllText(Path.Combine(output, "README.txt"), "Synthetic in-memory data; WpfTestHost Application, never App.OnStartup. PNG rendering is not physical DPI or interactive acceptance.");
    });

    [Fact]
    public void InteractiveSyntheticPreview() => WpfTestHost.Run(() =>
    {
        if (Environment.GetEnvironmentVariable("POKE_NIGHT_DIARY_INTERACTIVE") != "1") return;
        var data = State(); var view = View(data); Select(view, data); var menu = Menu(view, data);
        menu.Title = "Night Diary — SYNTHETIC PREVIEW — no saved profile";
        var frame = new DispatcherFrame();
        menu.Closed += (_, _) => frame.Continue = false;
        menu.Show(); Dispatcher.PushFrame(frame);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    });
}

