using System.Windows;

namespace PokeCorner.App.Popup;

public partial class AppMenuWindow : Window
{
    private readonly CatchView _catchView;

    public AppMenuWindow(CollectionView collectionView, SettingsView settingsView, ShopView shopView, CatchView catchView)
    {
        InitializeComponent();
        CollectionTab.Content = collectionView;
        SettingsTab.Content = settingsView;
        ShopTab.Content = shopView;
        CatchTab.Content = catchView;
        _catchView = catchView;
    }

    /// Drag and double-click-to-maximize for the custom title bar. WindowChrome's
    /// CaptionHeight is 0, so the OS does none of this for us any more.
    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximized();
            return;
        }

        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleMaximized();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    // Segoe MDL2 Assets glyphs, the same font (and codepoints) the real Windows title
    // bar draws its chrome buttons from.
    private const string MaximizeGlyph = "\uE922";
    private const string RestoreGlyph = "\uE923";

    private void ToggleMaximized()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        MaximizeButton.Content = WindowState == WindowState.Maximized ? RestoreGlyph : MaximizeGlyph;
        MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "Восстановить" : "Развернуть";
    }

    public void SelectTab(int index)
    {
        if (index >= 0 && index < MenuTabControl.Items.Count)
        {
            MenuTabControl.SelectedIndex = index;
        }
    }

    public async void ShowCatchReveal(CatchReveal reveal)
    {
        MenuTabControl.SelectedItem = CatchTab;
        await _catchView.PlayReveal(reveal);
    }
}
