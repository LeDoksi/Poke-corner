using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PokeCorner.Core;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

public partial class ShopView : System.Windows.Controls.UserControl
{
    private readonly AppState _appState;
    private readonly ShopService _shopService;
    private readonly Func<RarityTier, bool> _tryBuyEgg;
    private readonly Action _onStateChanged;
    private DispatcherTimer? _liveRefreshTimer;

    public ShopView(AppState appState, ShopService shopService, Func<RarityTier, bool> tryBuyEgg, Action onStateChanged)
    {
        InitializeComponent();
        _appState = appState;
        _shopService = shopService;
        _tryBuyEgg = tryBuyEgg;
        _onStateChanged = onStateChanged;
        Loaded += (_, _) => StartLiveRefresh();
        Unloaded += (_, _) => StopLiveRefresh();
        Refresh();
    }

    private void StartLiveRefresh()
    {
        _liveRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _liveRefreshTimer.Tick += (_, _) => Refresh();
        _liveRefreshTimer.Start();
    }

    private void StopLiveRefresh()
    {
        _liveRefreshTimer?.Stop();
        _liveRefreshTimer = null;
    }

    private void TreatPackButton_Click(object sender, RoutedEventArgs e) =>
        ReportResult(_shopService.BuyTreatPack(_appState.Shop, _appState.Feeding), "Куплена пачка лакомств.", "Не хватает монет.");

    private void CarePointsBoosterButton_Click(object sender, RoutedEventArgs e)
    {
        var active = _appState.Collection.GetActive();
        if (active is null)
        {
            MessageText.Text = "Нет активного питомца на рабочем столе.";
            return;
        }
        var bought = _shopService.BuyCarePointsBooster(_appState.Shop, active);
        if (bought) _appState.RecomputeStage(active);
        ReportResult(bought, "Бустер роста применён.", "Не хватает монет.");
    }

    private void CatchLuckyCharmButton_Click(object sender, RoutedEventArgs e) =>
        ReportResult(_shopService.BuyCatchLuckyCharm(_appState.Shop), "Талисман удачи активирован на следующую поимку.", "Не хватает монет.");

    private void EggCommonButton_Click(object sender, RoutedEventArgs e) => BuyEgg(RarityTier.Common);
    private void EggUncommonButton_Click(object sender, RoutedEventArgs e) => BuyEgg(RarityTier.Uncommon);
    private void EggRareButton_Click(object sender, RoutedEventArgs e) => BuyEgg(RarityTier.Rare);
    private void EggLegendaryButton_Click(object sender, RoutedEventArgs e) => BuyEgg(RarityTier.Legendary);

    private void BuyEgg(RarityTier tier) =>
        ReportResult(_tryBuyEgg(tier), "Яйцо отправлено в инкубацию — загляни в Коллекцию через час.", "Не хватает монет.");

    private void ReportResult(bool success, string successMessage, string failureMessage)
    {
        MessageText.Text = success ? successMessage : failureMessage;
        if (success) _onStateChanged();
        Refresh();
    }

    public void Refresh()
    {
        CoinsText.Text = $"Монеты: {_appState.Shop.Coins}";
        CoinProgressBar.Value = ProgressCalculator.ComputeCoinFraction(_appState.Shop);
        TreatPackButton.Content = $"Пачка лакомств (5 шт) — {ShopService.TreatPackPrice}";
        CarePointsBoosterButton.Content = $"Бустер роста (+{ShopService.CarePointsBoosterAmount} CP) — {ShopService.CarePointsBoosterPrice}";
        CatchLuckyCharmButton.Content = $"Талисман удачи — {ShopService.CatchLuckyCharmPrice}";
        EggCommonButton.Content = $"Яйцо: Обычное — {ShopService.EggPriceCommon}";
        EggUncommonButton.Content = $"Яйцо: Необычное — {ShopService.EggPriceUncommon}";
        EggRareButton.Content = $"Яйцо: Редкое — {ShopService.EggPriceRare}";
        EggLegendaryButton.Content = $"Яйцо: Легендарное — {ShopService.EggPriceLegendary}";
    }
}
