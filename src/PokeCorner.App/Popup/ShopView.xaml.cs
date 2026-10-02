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
    private readonly Func<RarityTier, bool> _isTierAvailable;
    private readonly Action _onStateChanged;
    private DispatcherTimer? _liveRefreshTimer;

    private const string UnavailableTierToolTip = "Пока недоступно — нужен более широкий каталог видов";

    public ShopView(AppState appState, ShopService shopService, Func<RarityTier, bool> tryBuyEgg, Func<RarityTier, bool> isTierAvailable, Action onStateChanged)
    {
        InitializeComponent();
        _appState = appState;
        _shopService = shopService;
        _tryBuyEgg = tryBuyEgg;
        _isTierAvailable = isTierAvailable;
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
            ShowMessage("Нет активного питомца на рабочем столе.");
            return;
        }
        var bought = _shopService.BuyCarePointsBooster(_appState.Shop, active);
        // No stage recompute here any more: the booster adds Care Points, and whether that
        // crosses an evolution threshold is now the player's call via the Collection tab's
        // "Эволюционировать!" button rather than something that happens behind their back.
        ReportResult(bought, "Бустер роста применён.", "Не хватает монет.");
    }

    private void CatchLuckyCharmButton_Click(object sender, RoutedEventArgs e) =>
        ReportResult(_shopService.BuyCatchLuckyCharm(_appState.Shop), "Талисман удачи активирован на следующую поимку или вылупление.", "Не хватает монет.");

    private void EggCommonButton_Click(object sender, RoutedEventArgs e) => BuyEgg(RarityTier.Common);
    private void EggUncommonButton_Click(object sender, RoutedEventArgs e) => BuyEgg(RarityTier.Uncommon);
    private void EggRareButton_Click(object sender, RoutedEventArgs e) => BuyEgg(RarityTier.Rare);
    private void EggLegendaryButton_Click(object sender, RoutedEventArgs e) => BuyEgg(RarityTier.Legendary);

    private void BuyEgg(RarityTier tier) =>
        ReportResult(_tryBuyEgg(tier), "Яйцо отправлено в инкубацию — загляни в Коллекцию через час.", "Не хватает монет.");

    private void ReportResult(bool success, string successMessage, string failureMessage)
    {
        ShowMessage(success ? successMessage : failureMessage);
        if (success) _onStateChanged();
        Refresh();
    }

    private void ShowMessage(string message)
    {
        MessageText.Text = message;
        MessagePanel.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    public void Refresh()
    {
        CoinsText.Text = _appState.Shop.Coins.ToString(System.Globalization.CultureInfo.CurrentCulture);
        CoinProgressBar.Value = ProgressCalculator.ComputeCoinFraction(_appState.Shop);

        // Each card carries its name and its price in separate elements now (the price sits
        // in its own coin badge), so Refresh writes two TextBlocks per item instead of one
        // combined Button.Content string.
        TreatPackNameText.Text = "Пачка лакомств (5 шт)";
        TreatPackPriceText.Text = ShopService.TreatPackPrice.ToString(System.Globalization.CultureInfo.CurrentCulture);
        CarePointsBoosterNameText.Text = $"Бустер роста (+{ShopService.CarePointsBoosterAmount:N0} CP)";
        CarePointsBoosterPriceText.Text = ShopService.CarePointsBoosterPrice.ToString(System.Globalization.CultureInfo.CurrentCulture);
        // Self-describing name, same pattern the other two consumables already use ("Пачка
        // лакомств (5 шт)", "Бустер роста (+N CP)"). This card said only "Талисман удачи",
        // which left the one item whose effect isn't guessable as the one item with no
        // explanation. The multiplier is read from ShopService rather than written out, so it
        // can't drift from the number CatchRoller actually applies; the fuller wording lives
        // in the card's ToolTip in ShopView.xaml.
        CatchLuckyCharmNameText.Text = $"Талисман удачи (×{ShopService.CatchLuckyCharmShinyMultiplier:0} шанс шайни)";
        CatchLuckyCharmPriceText.Text = ShopService.CatchLuckyCharmPrice.ToString(System.Globalization.CultureInfo.CurrentCulture);

        EggCommonNameText.Text = "Обычное яйцо";
        EggCommonPriceText.Text = ShopService.EggPriceCommon.ToString(System.Globalization.CultureInfo.CurrentCulture);
        EggUncommonNameText.Text = "Необычное яйцо";
        EggUncommonPriceText.Text = ShopService.EggPriceUncommon.ToString(System.Globalization.CultureInfo.CurrentCulture);
        EggRareNameText.Text = "Редкое яйцо";
        EggRarePriceText.Text = ShopService.EggPriceRare.ToString(System.Globalization.CultureInfo.CurrentCulture);
        EggLegendaryNameText.Text = "Легендарное яйцо";
        EggLegendaryPriceText.Text = ShopService.EggPriceLegendary.ToString(System.Globalization.CultureInfo.CurrentCulture);

        // Buying an egg of a tier the current species catalog can't fill would take the
        // coins and then leave an unhatchable egg in the save file forever, so those
        // buttons are disabled outright rather than merely failing on click. The whole card
        // dims with them, so an unavailable tier reads as unavailable at a glance rather
        // than looking like a live card with one dead button.
        ApplyTierAvailability(EggCommonButton, EggCommonCard, RarityTier.Common);
        ApplyTierAvailability(EggUncommonButton, EggUncommonCard, RarityTier.Uncommon);
        ApplyTierAvailability(EggRareButton, EggRareCard, RarityTier.Rare);
        ApplyTierAvailability(EggLegendaryButton, EggLegendaryCard, RarityTier.Legendary);
    }

    private void ApplyTierAvailability(System.Windows.Controls.Button button, Border card, RarityTier tier)
    {
        var available = _isTierAvailable(tier);
        button.IsEnabled = available;
        button.ToolTip = available ? null : UnavailableTierToolTip;
        card.Opacity = available ? 1.0 : 0.45;
        card.ToolTip = available ? null : UnavailableTierToolTip;
    }
}
