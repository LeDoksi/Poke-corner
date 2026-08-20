using System.Windows;

namespace PokeCorner.App.Popup;

public partial class AppMenuWindow : Window
{
    public AppMenuWindow(CollectionView collectionView, SettingsView settingsView, ShopView shopView)
    {
        InitializeComponent();
        CollectionTab.Content = collectionView;
        SettingsTab.Content = settingsView;
        ShopTab.Content = shopView;
    }

    public void SelectTab(int index)
    {
        if (index >= 0 && index < MenuTabControl.Items.Count)
        {
            MenuTabControl.SelectedIndex = index;
        }
    }
}
