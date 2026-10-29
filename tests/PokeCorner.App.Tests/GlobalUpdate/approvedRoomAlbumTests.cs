using System.IO;
using System.Windows;
using System.Windows.Controls;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Tests.GlobalUpdate;

public class ApprovedRoomAlbumTests
{
    private static string DataRoot => Path.Combine(AppContext.BaseDirectory, "Data");
    private static readonly Lazy<RoomArt> Art = new(() => RoomArt.Load(DataRoot));
    private static readonly Lazy<CatalogPackage> Catalog = new(() => CatalogPackage.Load(DataRoot));
    private static PokemonIndividual Pet() => new()
    {
        SpeciesId = 133, CurrentSpeciesId = 133, FamilyRootId = 133, EvolutionChainId = 67,
        ChainLength = 2, Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow,
        Nickname = "Оченьдлинноерусскоеимя", TotalXp = 420, AccumulatedCarePoints = 12000
    };
    private static SaveData State(PokemonIndividual? pet = null) => new()
    {
        SchemaVersion = 2, Individuals = pet is null ? [] : [pet], ActiveIndividualId = pet?.Id,
        Settings = new(), FeedingTreats = 12
    };

    [Fact]
    public void CornerExposesCareXpAndReachablePrimaryActionsWithLongNickname() => WpfTestHost.Run(() =>
    {
        var pet = Pet();
        var data = State(pet);
        var view = new RoomView(Art.Value, () => data, () => null, () => { }, id => "Иви", Catalog.Value);
        WpfTestHost.Layout(view, 800, 490);
        Assert.Contains(pet.Nickname!, ((TextBlock)view.FindName("PetCaption")).Text);
        Assert.NotNull(view.FindName("CareText"));
        Assert.Null(view.FindName("XpText"));
        var caption = ((TextBlock)view.FindName("PetCaption")).Text;
        Assert.Contains("№0133", caption);
        Assert.Contains("Нормальный", caption);
        Assert.Contains("Необычный", caption);
        Assert.Contains("Ур.", caption);
        Assert.DoesNotContain("%", ((TextBlock)view.FindName("CareText")).Text);
        Assert.Contains("12", ((TextBlock)view.FindName("TreatText")).Text);
        foreach (var name in new[] { "PetButton", "FeedButton", "DetailButton", "AchievementsButton" })
        {
            var button = Assert.IsType<Button>(view.FindName(name));
            Assert.Equal(Visibility.Visible, button.Visibility);
            Assert.True(button.ActualWidth > 0 && button.ActualHeight > 0);
            var bounds = button.TransformToAncestor(view).TransformBounds(new Rect(button.RenderSize));
            Assert.InRange(bounds.Bottom, 0, 490);
            Assert.InRange(bounds.Right, 0, 800);
        }
    });

    [Fact]
    public void RoomSpriteSizeIsIndependentFromDesktopPreference() => WpfTestHost.Run(() =>
    {
        var pet = Pet();
        var data = State(pet);
        var frames = GifFrameDecoder.DecodeFramesAsPng(File.ReadAllBytes(Catalog.Value.GetAssetPath(133, false)!));
        var view = new RoomView(Art.Value, () => data, () => frames, () => { });
        var image = (Image)view.FindName("PetImage");
        var width = image.Width;
        var source = image.Source;
        data.Settings.PetSizePixels = 384;
        view.Refresh();
        Assert.Equal(width, image.Width);
        Assert.Same(source, image.Source);
        WpfTestHost.Layout(view, 1000, 650);
        var visibleWidth = image.TransformToAncestor(view).TransformBounds(new Rect(0, 0, image.Width, image.Height)).Width;
        Assert.InRange(visibleWidth, 145, 160);
        Assert.Equal(384, data.Settings.PetSizePixels);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletedCareDoesNotClaimEvolutionReadyWhenCanonicalRecipeIsBlocked(bool unavailableBranches) => WpfTestHost.Run(() =>
    {
        var pet = Pet();
        pet.AccumulatedCarePoints = EvolutionThresholds.Stage2Threshold;
        var data = State(pet);
        var catalog = CatalogPackage.Load(DataRoot);
        var evolution = new EvolutionService(catalog, data);
        var preview = evolution.Preview(pet.Id);
        if (unavailableBranches)
        {
            data.Berries[preview.BerryId] = preview.BerryCost;
            foreach (var branch in preview.Branches)
                catalog.Species.Single(species => species.Id == branch.SpeciesId).NormalAvailable = false;
            preview = evolution.Preview(pet.Id);
            Assert.All(preview.Branches, branch => Assert.False(branch.Available));
        }
        else
        {
            Assert.Equal(0, preview.BerriesOwned);
            Assert.Contains(preview.Branches, branch => branch.Available);
        }
        Assert.NotNull(preview.Reason);
        var view = new RoomView(Art.Value, () => data, () => null, () => { }, catalog: catalog);
        var caption = ((TextBlock)view.FindName("CareText")).Text;
        Assert.DoesNotContain("Готов к эволюции", caption);
        Assert.Contains("Уход накоплен", caption);
        Assert.Equal(1, ((System.Windows.Controls.ProgressBar)view.FindName("CareBar")).Value);
    });

    [Fact]
    public void ButtonsTargetCurrentPetAndEmptyStateOpensPokedex() => WpfTestHost.Run(() =>
    {
        var pet = Pet();
        var data = State(pet);
        Guid? opened = null;
        var petted = 0;
        var fed = 0;
        var pokedex = 0;
        var albums = 0;
        var view = new RoomView(Art.Value, () => data, () => null, () => albums++, catalog: Catalog.Value,
            pet: () => petted++, feed: () => fed++, openIndividual: id => opened = id, openPokedex: () => pokedex++);
        Click(view, "PetButton");
        Click(view, "FeedButton");
        Click(view, "DetailButton");
        Click(view, "AchievementsButton");
        Assert.Equal((1, 1, 1), (petted, fed, albums));
        Assert.Equal(pet.Id, opened);
        Assert.Equal(Visibility.Visible, ((TextBlock)view.FindName("MissingSpriteText")).Visibility);
        data.ActiveIndividualId = null;
        view.Refresh();
        Assert.False(((Button)view.FindName("PetButton")).IsEnabled);
        Assert.False(((Button)view.FindName("FeedButton")).IsEnabled);
        Click(view, "DetailButton");
        Assert.Equal(1, pokedex);
        Assert.Equal(Visibility.Collapsed, ((TextBlock)view.FindName("MissingSpriteText")).Visibility);
    });

    [Fact]
    public void UnloadedRoomStopsPollingAndLoadedRoomResumes() => WpfTestHost.Run(() =>
    {
        var reads = 0;
        var data = State(Pet());
        var view = new RoomView(Art.Value, () => { reads++; return data; }, () => null, () => { });
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Pump(TimeSpan.FromMilliseconds(1150));
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var stopped = reads;
        Pump(TimeSpan.FromMilliseconds(1150));
        Assert.Equal(stopped, reads);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Assert.True(reads > stopped);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    });

    [Fact]
    public void ShelfDisablesDuringWriteAndFailureRetainsLiveStateForRetry() => WpfTestHost.Run(() =>
    {
        var live = State();
        live.AwardLedger["trophy-species-1"] = new();
        live.AwardLedger["trophy-care-1"] = new();
        live.TrophyShelfIds = ["trophy-care-1"];
        var pending = new TaskCompletionSource();
        var fail = true;
        var calls = 0;
        async Task Commit(Action<SaveData> edit)
        {
            calls++;
            var next = JsonSaveStore.Clone(live);
            edit(next);
            await pending.Task;
            if (fail) throw new IOException("synthetic write failure");
            live = next;
        }
        var view = new AchievementsView(Art.Value, () => live, Commit, () => { }, new AchievementService(Catalog.Value));
        Button Card(int index) => (Button)((StackPanel)((WrapPanel)view.FindName("CardsPanel")).Children[index]).Children[^1];
        Button Slot() => (Button)((StackPanel)((System.Windows.Controls.Primitives.UniformGrid)view.FindName("ShelfPanel")).Children[0]).Children[^1];
        Card(0).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Slot().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(Slot().IsEnabled);
        Assert.False(Card(0).IsEnabled);
        Assert.Equal("trophy-care-1", live.TrophyShelfIds[0]);
        pending.SetResult();
        Pump(TimeSpan.FromMilliseconds(20));
        Assert.Contains("synthetic write failure", ((TextBlock)view.FindName("MessageText")).Text);
        Assert.Equal("trophy-care-1", live.TrophyShelfIds[0]);
        fail = false;
        Slot().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("trophy-species-1", live.TrophyShelfIds[0]);
        Assert.Equal(2, calls);
        Assert.Equal(2, live.AwardLedger.Count);
        Assert.Equal(0, live.ShopCoins);
    });

    private static void Click(FrameworkElement view, string name) =>
        ((Button)view.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void Pump(TimeSpan duration)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    [Fact]
    public void AlbumShowsCanonicalNextGoalAndUnknownHistoricalMetricWithoutGranting() => WpfTestHost.Run(() =>
    {
        var data = State();
        data.DiscoveredSpeciesIds.UnionWith(Enumerable.Range(1, 12));
        data.AwardLedger["trophy-species-1"] = new();
        data.Metrics["successful-catches"] = null;
        var before = JsonSaveStore.Clone(data);
        var album = new AchievementsView(Art.Value, () => data, _ => Task.CompletedTask, () => { });
        var cards = (WrapPanel)album.FindName("CardsPanel");
        var species = (StackPanel)cards.Children[0];
        Assert.Contains(species.Children.OfType<TextBlock>(), text => text.Text.Contains("12 / 25"));
        var catches = (StackPanel)cards.Children[2];
        Assert.Contains(catches.Children.OfType<TextBlock>(), text => text.Text.Contains("неизвестен"));
        Assert.Equal(before.ShopCoins, data.ShopCoins);
        Assert.Equal(before.AwardLedger.Keys, data.AwardLedger.Keys);
        Assert.Null(data.Metrics["successful-catches"]);
    });

    [Fact]
    public void Fix1CornerFastGrowerCareBoundaryRemainsCareSpecificAndTerminalSafe() => WpfTestHost.Run(() =>
    {
        var pet = Pet(); pet.FirstTraitId = "fast-grower"; pet.AccumulatedCarePoints = 66499;
        var data = State(pet); var view = new RoomView(Art.Value, () => data, () => null, () => {}, catalog: Catalog.Value);
        Assert.True(((System.Windows.Controls.ProgressBar)view.FindName("CareBar")).Value < 1);
        pet.AccumulatedCarePoints = 66500; view.Refresh();
        Assert.Equal(1d, ((System.Windows.Controls.ProgressBar)view.FindName("CareBar")).Value);
        var text = ((TextBlock)view.FindName("CareText")).Text; Assert.Contains("Уход накоплен", text); Assert.DoesNotContain("Готов к эволюции", text); Assert.DoesNotContain("%", text);
        pet.CurrentStage = EvolutionStage.Stage2; view.Refresh();
        Assert.Equal(EvolutionProgressDisplay.FinalFormLabel, ((TextBlock)view.FindName("CareText")).Text); Assert.Equal(1d, ((System.Windows.Controls.ProgressBar)view.FindName("CareBar")).Value);
        var standalone = new PokemonIndividual { SpeciesId = 143, CurrentSpeciesId = 143, FamilyRootId = 143, EvolutionChainId = 72, ChainLength = 1,
            Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow, FirstTraitId = "fast-grower", AccumulatedCarePoints = 196000 };
        data = State(standalone); view.Refresh(); Assert.Equal(EvolutionProgressDisplay.NeverEvolvesLabel, ((TextBlock)view.FindName("CareText")).Text);
        Assert.Equal(0d, ((System.Windows.Controls.ProgressBar)view.FindName("CareBar")).Value);
    });
}
