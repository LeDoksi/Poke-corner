using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PokeCorner.Core;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

public partial class CollectionView : System.Windows.Controls.UserControl
{
    private readonly CollectionViewModel _viewModel;
    private readonly AppState _appState;
    private readonly Action _onPetRequested;
    private readonly Action _onFeedRequested;
    private readonly Action _onStateChanged;
    private readonly Func<byte[]?> _getActiveSpriteBytes;
    private readonly Func<Guid, bool> _onHatchRequested;
    private DispatcherTimer? _liveRefreshTimer;

    public CollectionView(CollectionViewModel viewModel, AppState appState, Action onPetRequested, Action onFeedRequested, Action onStateChanged, Func<byte[]?> getActiveSpriteBytes, Func<Guid, bool> onHatchRequested)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _appState = appState;
        _onPetRequested = onPetRequested;
        _onFeedRequested = onFeedRequested;
        _onStateChanged = onStateChanged;
        _getActiveSpriteBytes = getActiveSpriteBytes;
        _onHatchRequested = onHatchRequested;
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

    private void PetButton_Click(object sender, RoutedEventArgs e)
    {
        _onPetRequested();
        _onStateChanged();
        Refresh();
    }

    private void FeedButton_Click(object sender, RoutedEventArgs e)
    {
        _onFeedRequested();
        _onStateChanged();
        Refresh();
    }

    public void Refresh()
    {
        var active = _appState.Collection.GetActive();
        CarePointsText.Text = active is null ? "Нет активного питомца" : $"Care Points: {active.AccumulatedCarePoints}";
        EntriesList.ItemsSource = _viewModel.GetEntries(DateTimeOffset.UtcNow);

        var progressToNextTreat = _appState.Feeding.RemainderUnits;
        TreatsText.Text = $"Лакомства: {_appState.Feeding.Treats} (ещё {FeedingService.ActivityUnitsPerTreatEarned - progressToNextTreat} активности до следующего)";

        var progress = ProgressCalculator.Compute(_appState.Activity, active, _appState.Feeding);
        CatchProgressBar.Value = progress.CatchFraction;
        EvolutionProgressBar.Value = progress.EvolutionFraction;
        TreatProgressBar.Value = progress.TreatFraction;

        var spriteBytes = _getActiveSpriteBytes();
        ActiveSpriteImage.Source = spriteBytes is null ? null : DecodePng(spriteBytes);

        PendingEggsList.ItemsSource = _viewModel.GetPendingEggEntries(DateTimeOffset.UtcNow);

        var pokedex = _viewModel.GetPokedexEntries();
        PokedexCountText.Text = $"Pokédex: {pokedex.Count} species";
        PokedexGrid.ItemsSource = pokedex;
    }

    private static System.Windows.Media.ImageSource DecodePng(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    // Fully qualified as System.Windows.Controls.Button, not bare Button — Task 5 turned
    // on UseWindowsForms for TrayIconService's NotifyIcon, which makes Button ambiguous
    // with System.Windows.Forms.Button project-wide from that task onward (the same
    // Application-ambiguity issue Task 5 hit, just a different colliding type name).
    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        var id = (Guid)((System.Windows.Controls.Button)sender).Tag;
        if (_viewModel.TrySetActive(id, DateTimeOffset.UtcNow))
        {
            _onStateChanged();
        }
        Refresh();
    }

    private void ReleaseButton_Click(object sender, RoutedEventArgs e)
    {
        var id = (Guid)((System.Windows.Controls.Button)sender).Tag;
        _viewModel.Release(id);
        _onStateChanged();
        Refresh();
    }

    private void HatchButton_Click(object sender, RoutedEventArgs e)
    {
        var eggId = (Guid)((System.Windows.Controls.Button)sender).Tag;
        if (_onHatchRequested(eggId))
        {
            _onStateChanged();
        }
        Refresh();
    }
}
