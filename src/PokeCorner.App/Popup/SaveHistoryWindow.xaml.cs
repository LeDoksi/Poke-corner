using System.Windows.Controls;
using PokeCorner.Core.Persistence;

namespace PokeCorner.App.Popup;

public partial class SaveHistoryWindow : System.Windows.Window
{
    public SaveHistoryEntry? SelectedEntry { get; private set; }
    public SaveHistoryWindow(IReadOnlyList<SaveHistoryEntry> entries, int unreadableCount)
    {
        InitializeComponent();
        HistoryList.ItemsSource = entries;
        Explanation.Text = entries.Count == 0 ? "Исправные резервные копии не найдены. Исходные файлы сохранены."
            : "Выберите дату до потери прогресса. Восстановление заменит текущую игру и перезапустит приложение. Текущие данные останутся в истории.";
        if (unreadableCount > 0) Explanation.Text += $" Недоступных или повреждённых копий: {unreadableCount}.";
    }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) => RestoreButton.IsEnabled = HistoryList.SelectedItem is SaveHistoryEntry;
    private void Restore_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is not SaveHistoryEntry entry) return;
        if (!ConfirmDialog.Ask("Восстановить сохранение?", entry + "\nТекущая игра будет заменена этой копией. Исходные данные сохранятся в истории.", "Восстановить", this)) return;
        SelectedEntry = entry; DialogResult = true;
    }
    private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e) => DialogResult = false;
}
