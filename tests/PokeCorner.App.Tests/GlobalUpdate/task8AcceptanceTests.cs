using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using PokeCorner.App.Onboarding;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Tests.GlobalUpdate;

public class Task8AcceptanceTests
{
    [Theory]
    [InlineData("000000", false)]
    [InlineData("#000000", false)]
    [InlineData("#0001", true)]
    [InlineData("№0001", true)]
    public void ExpeditionNumericSearchRetainsPaddedIdsWithoutZeroWildcard(string query, bool found) => WpfTestHost.Run(() =>
    {
        var data = ApprovedExpeditionTests.State(1);
        data.Individuals[0].CurrentSpeciesId = 1;
        var view = ApprovedExpeditionTests.View(data);
        ApprovedExpeditionTests.Click(view, "ChooseTeamButton");
        ApprovedExpeditionTests.Find<TextBox>(view, "PickerSearchBox").Text = query;
        var rows = ApprovedExpeditionTests.Find<WrapPanel>(view, "PickerMembers");
        Assert.Equal(found, rows.Children.Count == 1);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    });

    [Fact]
    public void CompletedGiftTourUsesOpenedBallWithoutInventingAnotherGift() => WpfTestHost.Run(() =>
    {
        var window = new OnboardingWindow(6, giftQueueCount: () => 0);
        try
        {
            var illustration = (StackPanel)window.FindName("Illustration");
            var image = Assert.Single(illustration.Children.OfType<Image>());
            Assert.NotNull(image.Source);
            Assert.Contains("уже открыты", ((TextBlock)window.FindName("BodyText")).Text);
        }
        finally { window.Close(); }
    });

    [Fact]
    public async Task SharedArtProviderDoesNotRepresentRetainedUnsupportedShinyWithNormalArt()
    {
        var catalog = ApprovedExpeditionTests.Catalog.Value;
        var unsupported = catalog.Species.First(s => catalog.GetAssetPath(s.Id, false) is not null && catalog.GetAssetPath(s.Id, true) is null);
        var app = (global::PokeCorner.App.App)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(global::PokeCorner.App.App));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(global::PokeCorner.App.App).GetField("_catalogPackage", flags)!.SetValue(app, catalog);
        typeof(global::PokeCorner.App.App).GetField("_decodedFramesCache", flags)!.SetValue(app, new Dictionary<(int, bool), IReadOnlyList<byte[]>?>());
        var fetch = typeof(global::PokeCorner.App.App).GetMethod("GetAnimatedFramesAsync", flags, [typeof(int), typeof(bool)])!;
        Assert.Null(await (Task<IReadOnlyList<byte[]>?>)fetch.Invoke(app, [unsupported.Id, true])!);
    }

    [Fact]
    public async Task TwoSuccessfulSameItemPurchasesUseDistinctCommittedOperations()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Task8-shop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var catalog = ApprovedExpeditionTests.Catalog.Value;
        var data = SaveMigration.Migrate(new SaveData { Individuals = [], Settings = new() }, LegacyLookup.Load(catalog.DataRoot), catalog);
        data.ShopCoins = 100;
        var store = new JsonSaveStore(Path.Combine(directory, "state.json")); store.Save(data);
        var state = new AppState(); state.Hydrate(data);
        var writer = new BackgroundSaveWriter(store);
        ShopView view = null!;
        try
        {
            var shop = new ShopService(catalog.DataRoot);
            WpfTestHost.Run(() => view = new ShopView(state, shop, update => state.CommitAsync(writer, update, () => 0),
                () => { }, catalog, (_, _, _) => Task.FromResult(true)));
            for (var i = 0; i < 2; i++)
            {
                Task purchase = null!;
                WpfTestHost.Run(() => purchase = (Task)typeof(ShopView).GetMethod("Buy", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(view, [shop.Goods.Single(g => g.Id == "oran")])!);
                await purchase;
            }
            Assert.Equal(2, state.Shop.Berries["oran"]);
            Assert.Equal(2, store.Load()!.CompletedOperations.Count);
            Assert.Equal(90, state.Shop.Coins);
        }
        finally
        {
            WpfTestHost.Run(() => view?.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)));
            writer.Stop(); Directory.Delete(directory, true);
        }
    }
}
