using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rectangle = System.Windows.Shapes.Rectangle;
using PokeCorner.App.Popup;
using PokeCorner.Core;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Tests.GlobalUpdate;

public sealed class RoomAlbumTests
{
    private static string DataRoot => Path.Combine(AppContext.BaseDirectory, "Data");
    private static readonly Lazy<RoomArt> Art = new(() => RoomArt.Load(DataRoot));

    [Fact]
    public void ShelfStartsEmptyAndRoundTripsAsStableAwardIds()
    {
        var save = new SaveData { Individuals = [], Settings = new() };
        Assert.Empty(save.TrophyShelfIds);
        save.TrophyShelfIds.Add("trophy-species-1");
        var copy = JsonSaveStore.Clone(save);
        Assert.Equal(new[] { "trophy-species-1" }, copy.TrophyShelfIds);
    }

    [Fact]
    public void PackagedAwardsHave168ValidPlacementsAndExactMaskAlpha() => WpfTestHost.Run(() =>
    {
        var art = Art.Value;
        Assert.Equal(56, art.Awards.Count);
        Assert.Equal(26, art.Cards.Count);
        Assert.Equal(new[] { (664d, 280d), (810d, 280d), (956d, 280d) }, art.Anchors);
        Assert.Equal((590d, 281d, 441d, 7d), art.FrontlipRect);
        foreach (var award in art.Awards.Values)
        {
            var image = Read(art.PathFor(award.File));
            var mask = Read(art.PathFor(award.MaskFile));
            Assert.Equal((int)award.Width, image.PixelWidth);
            Assert.Equal((int)award.Height, image.PixelHeight);
            Assert.Equal(image.PixelWidth, mask.PixelWidth);
            Assert.Equal(image.PixelHeight, mask.PixelHeight);
            var imagePixels = Pixels(image);
            var maskPixels = Pixels(mask);
            for (var i = 3; i < imagePixels.Length; i += 4)
                Assert.Equal(imagePixels[i], maskPixels[i]);
            for (var slot = 0; slot < 3; slot++)
            {
                var (x, y) = art.Position(award, slot);
                var bounds = new[] { (592d, 183d, 735d, 284d), (737d, 183d, 883d, 284d), (885d, 183d, 1028d, 284d) }[slot];
                Assert.InRange(x, bounds.Item1, bounds.Item3);
                Assert.InRange(y, bounds.Item2, bounds.Item4);
                Assert.InRange(x + award.Width, bounds.Item1, bounds.Item3);
                Assert.InRange(y + award.Height, bounds.Item2, bounds.Item4);
            }
        }
    });

    [Fact]
    public void AlbumShowsLatestEarnedTierOrOnlyFirstMaskAndRejectsLockedSelection() => WpfTestHost.Run(() =>
    {
        var data = new SaveData { Individuals = [], Settings = new() };
        data.AwardLedger["trophy-species-1"] = new();
        data.AwardLedger["trophy-species-2"] = new();
        Assert.Equal("trophy-species-2", Art.Value.LatestEarned(data, "trophy-species-1"));
        Assert.Null(Art.Value.LatestEarned(data, "trophy-nursery-1"));
        Assert.Throws<InvalidOperationException>(() =>
            AchievementsView.ApplyShelfSelection(data, Art.Value, "trophy-nursery-1", 0));
        Assert.Empty(data.TrophyShelfIds);
        var album = new AchievementsView(Art.Value, () => data, _ => Task.CompletedTask, () => { });
        WpfTestHost.Layout(album, 640, 480);
        var cards = (WrapPanel)album.FindName("CardsPanel");
        Assert.Equal(26, cards.Children.Count);
        Assert.IsType<Rectangle>(((StackPanel)cards.Children[7]).Children[0]);
        Assert.IsType<Image>(((StackPanel)cards.Children[0]).Children[0]);
        var select = (Button)((StackPanel)cards.Children[0]).Children[^1];
        select.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        select = (Button)((StackPanel)cards.Children[0]).Children[^1];
        Assert.Equal("Отменить выбор", select.Content);
        select.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        select = (Button)((StackPanel)cards.Children[0]).Children[^1];
        Assert.Equal("Выбрать для полки", select.Content);
    });

    [Fact]
    public void ManualPlacementReplacementAndRemovalAreFreeAndKeepHistory()
    {
        var data = new SaveData { Individuals = [], Settings = new() };
        foreach (var id in new[] { "trophy-species-1", "trophy-care-1", "trophy-shiny-1", "trophy-special-nick" })
            data.AwardLedger[id] = new() { CoinsGranted = 7, CatalogRevision = "old" };
        AchievementsView.ApplyShelfSelection(data, Art.Value, "trophy-species-1", 0);
        AchievementsView.ApplyShelfSelection(data, Art.Value, "trophy-care-1", 1);
        AchievementsView.ApplyShelfSelection(data, Art.Value, "trophy-shiny-1", 2);
        AchievementsView.ApplyShelfSelection(data, Art.Value, "trophy-special-nick", 1);
        Assert.Equal(new[] { "trophy-species-1", "trophy-special-nick", "trophy-shiny-1" }, data.TrophyShelfIds);
        AchievementsView.ApplyShelfSelection(data, Art.Value, null, 1);
        Assert.Null(data.TrophyShelfIds[1]);
        AchievementsView.ApplyShelfSelection(data, Art.Value, "trophy-species-1", 1);
        Assert.Null(data.TrophyShelfIds[0]);
        Assert.Equal("trophy-species-1", data.TrophyShelfIds[1]);
        Assert.Equal(4, data.AwardLedger.Count);
        Assert.Equal(0, data.ShopCoins);
        Assert.All(data.AwardLedger.Values, entry => Assert.Equal("old", entry.CatalogRevision));
    }

    [Fact]
    public void NewAwardsFillFreePlacesButNeverRefillRemovedOrDisplaceFullShelf()
    {
        var catalog = CatalogPackage.Load(DataRoot);
        var service = new AchievementService(catalog);
        var data = new SaveData { SchemaVersion = 2, Individuals = [], Settings = new() };
        data.DiscoveredSpeciesIds.Add(1);
        service.Evaluate(data);
        Assert.Equal("trophy-species-1", data.TrophyShelfIds[0]);
        var history = data.AwardLedger["trophy-species-1"];
        data.DiscoveredSpeciesIds.UnionWith(Enumerable.Range(2, 24));
        service.Evaluate(data);
        Assert.Equal("trophy-species-2", data.TrophyShelfIds[0]);
        Assert.Same(history, data.AwardLedger["trophy-species-1"]);
        data.TrophyShelfIds[0] = null;
        service.Evaluate(data);
        Assert.Null(data.TrophyShelfIds[0]);
        Assert.Same(history, data.AwardLedger["trophy-species-1"]);
        foreach (var id in new[] { "trophy-special-nick", "trophy-special-favorite", "trophy-special-retrain" })
            data.AwardLedger[id] = new();
        data.TrophyShelfIds = ["trophy-special-nick", "trophy-special-favorite", "trophy-special-retrain"];
        data.ShinySpeciesIds.Add(1);
        service.Evaluate(data);
        Assert.Contains("trophy-shiny-1", data.AwardLedger.Keys);
        Assert.Equal(new[] { "trophy-special-nick", "trophy-special-favorite", "trophy-special-retrain" }, data.TrophyShelfIds);
    }

    [Fact]
    public async Task FailedShelfWritePublishesNothingAndRetryKeepsAwardHistory()
    {
        var catalog = CatalogPackage.Load(DataRoot);
        var initial = new SaveData
        {
            SchemaVersion = 2, CatalogRevision = catalog.CatalogRevision, BalanceRevision = "test",
            MigrationId = SaveMigration.MigrationId, Individuals = [], Settings = new(),
            Berries = SaveMigration.BerryIds.ToDictionary(id => id, _ => 0),
            Items = SaveMigration.ItemIds.ToDictionary(id => id, _ => 0),
            BerryRemainders = SaveMigration.BerryIds.ToDictionary(id => id, _ => new RationalRemainder())
        };
        initial.AwardLedger["trophy-species-1"] = new() { CoinsGranted = 5, CatalogRevision = "old" };
        initial.AwardLedger["trophy-care-1"] = new() { CoinsGranted = 5, CatalogRevision = "old" };
        initial.TrophyShelfIds.Add("trophy-species-1");
        var app = new AppState { Achievements = new AchievementService(catalog) };
        app.Hydrate(initial);
        var path = Path.Combine(Path.GetTempPath(), "room-album-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new JsonSaveStore(path);
            store.Save(initial);
            Directory.CreateDirectory(path + ".tmp");
            var writer = new BackgroundSaveWriter(store);
            void Edit(SaveData next) => AchievementsView.ApplyShelfSelection(next, Art.Value, "trophy-care-1", 0);
            await Assert.ThrowsAnyAsync<IOException>(() => app.CommitAsync(writer, Edit, () => 0));
            Assert.Equal("trophy-species-1", app.ToSaveData(0).TrophyShelfIds[0]);
            Assert.Equal("trophy-species-1", store.Load()!.TrophyShelfIds[0]);
            Directory.Delete(path + ".tmp");
            var recovered = new JsonSaveStore(path);
            var retryWriter = new BackgroundSaveWriter(recovered);
            await app.CommitAsync(retryWriter, Edit, () => 0);
            await app.CommitAsync(retryWriter, Edit, () => 0);
            var saved = recovered.Load()!;
            Assert.Equal("trophy-care-1", saved.TrophyShelfIds[0]);
            Assert.Equal(2, saved.AwardLedger.Count);
            Assert.Equal("old", saved.AwardLedger["trophy-species-1"].CatalogRevision);
            Assert.Equal(0, saved.ShopCoins);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(path + ".tmp")) Directory.Delete(path + ".tmp");
        }
    }

    [Fact]
    public void RoomUsesPackagedLayersAndScalesPetWithoutChangingSavedSize() => WpfTestHost.Run(() =>
    {
        var data = new SaveData { Individuals = [], Settings = new() };
        data.AwardLedger["trophy-care-1"] = new();
        data.TrophyShelfIds.Add("trophy-care-1");
        data.Settings.PetSizePixels = 384;
        var room = new RoomView(Art.Value, () => data, () => null, () => { });
        WpfTestHost.Layout(room, 640, 360);
        Assert.Equal(1448, ((Canvas)room.FindName("Scene")).Width);
        Assert.Single(((Canvas)room.FindName("AwardLayer")).Children);
        Assert.Equal(384, ((Image)room.FindName("PetImage")).Width);
        Assert.Equal(384, data.Settings.PetSizePixels);
        foreach (var (width, height, scale) in new[] { (820, 420, 1.5), (640, 360, 2.0) })
        {
            WpfTestHost.Layout(room, width, height);
            var scene = (Canvas)room.FindName("Scene");
            var transform = scene.TransformToAncestor(room);
            var upperLeft = transform.Transform(new Point(0, 0));
            var lowerRight = transform.Transform(new Point(1448, 582));
            Assert.Equal(1448d / 582d, (lowerRight.X - upperLeft.X) / (lowerRight.Y - upperLeft.Y), 6);
            Assert.True(upperLeft.X >= -0.001 && upperLeft.Y >= -0.001);
            Assert.True(lowerRight.X <= width + 0.001 && lowerRight.Y <= height + 0.001);
            var bitmap = new RenderTargetBitmap((int)(width * scale), (int)(height * scale),
                96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(room);
            Assert.True(bitmap.PixelWidth > 0);
        }
    });

    [Fact]
    public void OneRoomButtonOpensHiddenAlbumPageAndReturnsToRoom() => WpfTestHost.Run(() =>
    {
        var state = new AppState();
        var data = new SaveData { Individuals = [], Settings = new() };
        var vm = new CollectionViewModel(state.Collection, state.CareStateByIndividualId,
            new CareModeService(), Array.Empty<SpeciesEvolutionChain>(), state.PendingEggs);
        var collection = new CollectionView(vm, state,
            new SpriteCache(Path.Combine(Path.GetTempPath(), "room-album-test", Guid.NewGuid().ToString("N"))),
            () => { }, () => { }, () => { }, () => null, () => true, () => true);
        AppMenuWindow? menu = null;
        var room = new RoomView(Art.Value, () => data, () => null,
            () => menu!.Navigate(MenuSection.Achievements));
        var album = new AchievementsView(Art.Value, () => data, _ => Task.CompletedTask,
            () => menu!.Navigate(MenuSection.Corner));
        menu = new AppMenuWindow(collection, new SettingsView(new SettingsViewModel(state.Settings), () => { }),
            new ShopView(state, new ShopService(), _ => Task.CompletedTask, () => { }),
            new CatchView(state, () => null, _ => Task.FromResult<byte[]?>(null), () => 0, () => { }, id => $"Pokemon #{id}"),
            roomView: room, achievementsView: album);
        menu.Navigate(MenuSection.Achievements);
        WpfTestHost.Layout((FrameworkElement)menu.Content, 640, 480);
        var nested = (TabControl)menu.FindName("CornerTabs");
        Assert.Same(album, nested.SelectedContent);
        Assert.True(album.ActualWidth > 0 && album.ActualHeight > 0);
        Assert.Equal(Visibility.Collapsed, ((TabItem)menu.FindName("AlbumTab")).Visibility);
        menu.Navigate(MenuSection.Corner);
        Assert.Same(room, nested.SelectedContent);
    });

    [Fact]
    public void NormalAndShinyPackagePathsAreDistinctForWideAndSmallSpecies()
    {
        var catalog = CatalogPackage.Load(DataRoot);
        foreach (var id in new[] { 25, 133, 143 })
        {
            var normal = catalog.GetAssetPath(id, false);
            var shiny = catalog.GetAssetPath(id, true);
            Assert.NotNull(normal);
            Assert.NotNull(shiny);
            Assert.NotEqual(normal, shiny);
            Assert.True(File.Exists(normal));
            Assert.True(File.Exists(shiny));
        }
    }

    private static BitmapImage Read(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(Path.GetFullPath(path));
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] Pixels(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        return pixels;
    }
}
