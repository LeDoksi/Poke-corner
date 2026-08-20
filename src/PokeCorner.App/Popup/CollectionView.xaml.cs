using System.Windows;
using System.Windows.Controls;
using PokeCorner.Core;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

public partial class CollectionView : System.Windows.Controls.UserControl
{
    private readonly CollectionViewModel _viewModel;
    private readonly AppState _appState;
    private readonly Action _onFeedRequested;
    private readonly Action _onStateChanged;

    public CollectionView(CollectionViewModel viewModel, AppState appState, Action onFeedRequested, Action onStateChanged)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _appState = appState;
        _onFeedRequested = onFeedRequested;
        _onStateChanged = onStateChanged;
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

        if (_appState.Settings.FeedingMode == FeedingMode.Economy)
        {
            var progressToNextTreat = _appState.Feeding.RemainderUnits;
            TreatsText.Text = $"Лакомства: {_appState.Feeding.Treats} (ещё {FeedingService.ActivityUnitsPerTreatEarned - progressToNextTreat} активности до следующего)";
            TreatsText.Visibility = Visibility.Visible;
        }
        else
        {
            TreatsText.Visibility = Visibility.Collapsed;
        }
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
}
