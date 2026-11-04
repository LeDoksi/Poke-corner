using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace PokeCorner.App.Tests.GlobalUpdate;

// D-28 (Georgy 01.11): evolution and retraining top up in place with the nursery's cart.
public sealed partial class EvolutionTests
{
    private static int GoodPrice(string id) => new ShopService().Goods.Single(g => g.Id == id).Price;

    private static (CollectionView View, AppState App, List<IReadOnlyList<(string Item, int Quantity, Guid Operation)>> Calls, List<MenuSection> Navigations)
        TopUpView(SaveData data, bool evolution)
    {
        var app = new AppState();
        app.Hydrate(data);
        var vm = new CollectionViewModel(app.Collection, app.CareStateByIndividualId, new CareModeService(),
            [], [], species: Catalog.Value.Species, discoveredSpeciesIds: () => app.DiscoveredSpeciesIds);
        var calls = new List<IReadOnlyList<(string Item, int Quantity, Guid Operation)>>();
        var navigations = new List<MenuSection>();
        var view = new CollectionView(vm, app,
            new SpriteCache(Path.Combine(Path.GetTempPath(), "top-up-view", Guid.NewGuid().ToString("N"))),
            () => { }, () => { }, () => { }, () => null, () => false, () => true,
            previewEvolution: evolution ? individualId => new EvolutionService(Catalog.Value, data).Preview(individualId) : null,
            evolveIndividual: evolution ? (_, _, _, _) => Task.FromResult(new EvolutionAction(false, "test")) : null,
            previewRetraining: evolution ? null : (individualId, slot) => new RetrainingService(Catalog.Value, data).Preview(individualId, slot),
            retrainIndividual: evolution ? null : (_, _, _) => Task.FromResult(new RetrainingAction(false, "test")),
            navigate: navigations.Add,
            buyItems: items =>
            {
                calls.Add(items);
                var shop = new ShopService();
                foreach (var (item, quantity, operation) in items)
                {
                    var bought = shop.TryPurchase(data, item, quantity, operation);
                    if (!bought.Success) throw new InvalidOperationException(bought.Message);
                }
                app.Shop.Coins = data.ShopCoins;
                return Task.CompletedTask;
            });
        Assert.True(view.OpenIndividual(data.Individuals[0].Id));
        WpfTestHost.Layout(view, 1064, 616);
        return (view, app, calls, navigations);
    }

    private static void Click(FrameworkElement scope, string name) =>
        ((Button)scope.FindName(name)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    [Fact]
    public void RetrainingTopUpBuysTheMissingBerriesInOneSaveAndUnlocksRetraining() => WpfTestHost.Run(() =>
    {
        var data = State(1);
        data.ShopCoins = 1000;
        data.Berries["rawst"] = 0;
        var (view, _, calls, navigations) = TopUpView(data, evolution: false);
        Click(view, "DetailRetrainingToggle");
        var missing = new RetrainingService(Catalog.Value, data).Preview(data.Individuals[0].Id, 1).Costs
            .Where(x => x.Owned < x.Required).ToDictionary(x => x.BerryId, x => x.Required - x.Owned);
        Assert.NotEmpty(missing);
        var shopButton = (Button)view.FindName("DetailRetrainingShopButton");
        Assert.Equal(Visibility.Visible, shopButton.Visibility);
        Assert.Equal($"Докупить · {missing.Sum(x => x.Value * GoodPrice(x.Key))} ●", shopButton.Content);
        Assert.False(((Button)view.FindName("DetailRetrainButton")).IsEnabled);
        Click(view, "DetailRetrainingShopButton");
        var cart = (TopUpCart)view.FindName("DetailTopUpCart");
        Assert.True(cart.IsOpen);
        Click(cart, "BuyButton");
        var bought = Assert.Single(calls);
        Assert.Equal(missing, bought.ToDictionary(x => x.Item, x => x.Quantity));
        Assert.False(cart.IsOpen);
        Assert.Empty(navigations);
        Assert.StartsWith("Куплено:", ((TextBlock)view.FindName("DetailErrorText")).Text);
        Assert.True(((Button)view.FindName("DetailRetrainButton")).IsEnabled);
        Assert.Equal(Visibility.Collapsed, shopButton.Visibility);
    });

    [Fact]
    public void RetrainingTopUpWithoutCoinsSaysSoAndKeepsTheShopAsAWayOut() => WpfTestHost.Run(() =>
    {
        var data = State(1);
        data.ShopCoins = 0;
        data.Berries["rawst"] = 0;
        var (view, _, calls, navigations) = TopUpView(data, evolution: false);
        Click(view, "DetailRetrainingToggle");
        Click(view, "DetailRetrainingShopButton");
        var cart = (TopUpCart)view.FindName("DetailTopUpCart");
        Assert.False(((Button)cart.FindName("BuyButton")).IsEnabled);
        Assert.Contains("Не хватает", ((TextBlock)cart.FindName("TotalText")).Text);
        Click(cart, "ShopButton");
        Assert.Empty(calls);
        Assert.Equal(MenuSection.Shop, Assert.Single(navigations));
    });

    [Fact]
    public void EvolutionTopUpBuysBerriesThenOffersTheStoneForMissingCare() => WpfTestHost.Run(() =>
    {
        var data = State(133);
        data.ShopCoins = 1000;
        data.Berries["oran"] = 2;
        data.Individuals[0].AccumulatedCarePoints = 0;
        var (view, _, calls, _) = TopUpView(data, evolution: true);
        Click(view, "DetailConditionsButton");
        var shopButton = (Button)view.FindName("DetailEvolutionShopButton");
        Assert.Equal($"Докупить · {8 * GoodPrice("oran")} ●", shopButton.Content);
        Click(view, "DetailEvolutionShopButton");
        var cart = (TopUpCart)view.FindName("DetailTopUpCart");
        Assert.True(cart.IsOpen);
        // Care is short, so the stone is offered too, but only on request (starts at zero).
        Assert.Equal(2, ((StackPanel)cart.FindName("RowsPanel")).Children.Count);
        Click(cart, "BuyButton");
        Assert.Equal(new[] { ("oran", 8) }, Assert.Single(calls).Select(x => (x.Item, x.Quantity)));
        Assert.Equal(10, data.Berries["oran"]);
        Assert.False(((Button)view.FindName("DetailEvolveButton")).IsEnabled);

        Assert.Equal($"Камень эволюции · {GoodPrice("evolution-stone")} ●", shopButton.Content);
        Click(view, "DetailEvolutionShopButton");
        Click(cart, "BuyButton");
        Assert.Equal(new[] { ("evolution-stone", 1) }, calls[1].Select(x => (x.Item, x.Quantity)));
        var stone = (CheckBox)view.FindName("DetailEvolutionStoneToggle");
        Assert.True(stone.IsEnabled);
        stone.IsChecked = true;
        Assert.True(((Button)view.FindName("DetailEvolveButton")).IsEnabled);
        Assert.Equal("В магазин", shopButton.Content);
    });

    [Fact]
    public void EvolutionShopButtonStaysTheOldShopJumpWithoutACart() => WpfTestHost.Run(() =>
    {
        // No cart wired (older callers): the button keeps its old job and label.
        var data = State(133);
        data.Berries["oran"] = 2;
        var app = new AppState();
        app.Hydrate(data);
        var vm = new CollectionViewModel(app.Collection, app.CareStateByIndividualId, new CareModeService(),
            [], [], species: Catalog.Value.Species, discoveredSpeciesIds: () => app.DiscoveredSpeciesIds);
        var view = new CollectionView(vm, app,
            new SpriteCache(Path.Combine(Path.GetTempPath(), "top-up-view", Guid.NewGuid().ToString("N"))),
            () => { }, () => { }, () => { }, () => null, () => false, () => true,
            previewEvolution: individualId => new EvolutionService(Catalog.Value, data).Preview(individualId),
            evolveIndividual: (_, _, _, _) => Task.FromResult(new EvolutionAction(false, "test")));
        Assert.True(view.OpenIndividual(data.Individuals[0].Id));
        Assert.Equal("В магазин", ((Button)view.FindName("DetailEvolutionShopButton")).Content);
    });
}
