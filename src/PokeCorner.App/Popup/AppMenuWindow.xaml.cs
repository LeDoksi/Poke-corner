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
