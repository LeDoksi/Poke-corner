using System.Windows;
using System.Windows.Controls;
using PokeCorner.Core.Persistence;

namespace PokeCorner.App.Popup;

public partial class SaveHistoryWindow : Window
{
    public SaveHistoryEntry? SelectedEntry { get; private set; }
    private readonly Func<string, string, string, Task<bool>> _confirm;
    private TaskCompletionSource<bool>? _localConfirmation;
    private bool _pending;
    private bool _closed;

    public SaveHistoryWindow(IReadOnlyList<SaveHistoryEntry> entries, int unreadableCount,
        Func<string, string, string, Task<bool>>? confirmAsync = null)
    {
        InitializeComponent();
        _confirm = confirmAsync ?? ConfirmLocally;
        LocalConfirmationHost.Completed += CompleteLocalConfirmation;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Escape) return;
            if (_localConfirmation is not null) CompleteLocalConfirmation(false);
            else Close();
            e.Handled = true;
        };
        Closed += (_, _) => { _closed = true; CompleteLocalConfirmation(false); };
        HistoryList.ItemsSource = entries;
        Explanation.Text = entries.Count == 0 ? "Исправные резервные копии не найдены. Исходные файлы сохранены."
            : "Выбери копию, если нужно вернуться назад. Перед восстановлением сохраним текущую коллекцию.";
        Diagnostics.Text = $"Недоступных или повреждённых копий: {unreadableCount}. Исходные файлы сохраняются.";
    }

    public FrameworkElement TakeHostedContent()
    {
        var content = (FrameworkElement)Content;
        Content = null;
        content.Unloaded += (_, _) => { _closed = true; CompleteLocalConfirmation(false); };
        return content;
    }

    private Task<bool> ConfirmLocally(string title, string body, string caption)
    {
        _localConfirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        LocalConfirmationHost.Present(title, body, caption);
        return _localConfirmation.Task;
    }

    private void CompleteLocalConfirmation(bool result)
    {
        var pending = _localConfirmation; _localConfirmation = null;
        LocalConfirmationHost.Visibility = Visibility.Collapsed;
        pending?.TrySetResult(result);
    }

    private void SelectionChanged(object sender, SelectionChangedEventArgs e) => RestoreButton.IsEnabled = !_pending && HistoryList.SelectedItem is SaveHistoryEntry;

    private async void Restore_Click(object sender, RoutedEventArgs e) => await SelectAsync();

    private async Task SelectAsync()
    {
        if (_pending || _closed || HistoryList.SelectedItem is not SaveHistoryEntry entry) return;
        _pending = true; RestoreButton.IsEnabled = false;
        try
        {
            var details = $"{entry.SavedAtUtc.ToLocalTime():dd.MM.yyyy HH:mm:ss} · {entry.CollectionCount} питомцев · {entry.Coins} монет.";
            if (!await _confirm("Вернуться к этой копии?", details + " Текущая игра будет заменена; данные останутся в истории.", "Продолжить") || _closed) return;
            if (!await _confirm("Восстановить сохранение?", details + " Приложение перезапустится с выбранной коллекцией.", "Восстановить") || _closed) return;
            SelectedEntry = entry;
            if (IsVisible) DialogResult = true;
            else Close();
        }
        catch (Exception error) { if (!_closed) ErrorText.Text = "Восстановление не выполнено: " + error.Message; }
        finally { _pending = false; if (!_closed) RestoreButton.IsEnabled = HistoryList.SelectedItem is SaveHistoryEntry; }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (IsVisible) DialogResult = false;
        else Close();
    }
}
