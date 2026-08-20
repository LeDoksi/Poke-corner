using System.Windows;

namespace PokeCorner.App.Popup;

public partial class AppMenuWindow : Window
{
    public AppMenuWindow(CollectionView collectionView, SettingsView settingsView)
    {
        InitializeComponent();
        CollectionTab.Content = collectionView;
        SettingsTab.Content = settingsView;
    }

    public void SelectTab(int index)
    {
        if (index >= 0 && index < MenuTabControl.Items.Count)
        {
            MenuTabControl.SelectedIndex = index;
        }
    }
}
