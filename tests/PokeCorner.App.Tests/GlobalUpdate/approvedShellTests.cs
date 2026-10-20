using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core;
using PokeCorner.Core.Persistence;

namespace PokeCorner.App.Tests.GlobalUpdate;

public class ApprovedShellTests
{
    private static AppMenuWindow CreateMenu() => ShellTests.CreateMenu(new AppState());

    private static bool CompletedResult(Task<bool> pending)
    {
        Assert.True(pending.IsCompletedSuccessfully);
        return pending.GetAwaiter().GetResult();
    }

    [Fact]
    public void OpenWalletRefreshStopsAfterWindowCloses() => WpfTestHost.Run(() =>
    {
        var snapshot = new AppState().ToSaveData(0);
        var reads = 0;
        var menu = ShellTests.CreateMenu(new AppState(), () => { reads++; return snapshot; });
        try
        {
            menu.Show();
            var nested = (TabControl)menu.FindName("CornerTabs");
            Assert.All(nested.Items.Cast<TabItem>(), tab => Assert.False(tab.IsVisible));
            snapshot.ShopCoins = 315;
            PumpFor(TimeSpan.FromMilliseconds(1150));
            Assert.Equal("315", ((TextBlock)menu.FindName("WalletText")).Text);
            menu.Close();
            var readsAtClose = reads;
            snapshot.ShopCoins = 500;
            PumpFor(TimeSpan.FromMilliseconds(1150));
            Assert.Equal(readsAtClose, reads);
        }
        finally { menu.Close(); }
    });

    private static void PumpFor(TimeSpan duration)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    [Fact]
    public void ConfirmationStartsOnCancelCyclesFocusAndEscapeRestoresInvoker() => WpfTestHost.Run(() =>
    {
        var menu = CreateMenu();
        try
        {
            menu.Show();
            menu.Activate();
            var invoker = (Button)menu.FindName("SettingsButton");
            invoker.Focus();
            var pending = menu.ConfirmAsync("Прервать?", "Действие отменится.", "Прервать");
            var overlay = (FrameworkElement)menu.FindName("ConfirmationHost");
            var cancel = (Button)overlay.FindName("CancelButton");
            var confirm = (Button)overlay.FindName("ConfirmButton");
            Assert.Same(cancel, Keyboard.FocusedElement);
            cancel.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            Assert.Same(confirm, Keyboard.FocusedElement);
            confirm.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            Assert.Same(cancel, Keyboard.FocusedElement);
            using var source = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Confirmation key test") { Width = 1, Height = 1 });
            var escape = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            menu.RaiseEvent(escape);
            Assert.True(escape.Handled);
            Assert.False(CompletedResult(pending));
            Assert.Same(invoker, Keyboard.FocusedElement);
            Assert.True(menu.IsVisible);
        }
        finally { menu.Close(); }
    });

    [Fact]
    public void SnapshotCallbackRefreshesWalletWhenNavigating() => WpfTestHost.Run(() =>
    {
        var snapshot = new AppState().ToSaveData(0);
        snapshot.ShopCoins = 125;
        var menu = ShellTests.CreateMenu(new AppState(), () => snapshot);
        Assert.Equal("125", ((TextBlock)menu.FindName("WalletText")).Text);
        snapshot.ShopCoins = 207;
        menu.Navigate(MenuSection.Nursery);
        Assert.Equal("207", ((TextBlock)menu.FindName("WalletText")).Text);
        menu.Close();
    });

    [Fact]
    public void ShellHasApprovedSizeAndOrderedNavigation() => WpfTestHost.Run(() =>
    {
        var menu = CreateMenu();
        Assert.Equal(Math.Min(1000, SystemParameters.WorkArea.Width), menu.Width);
        Assert.Equal(Math.Min(760, SystemParameters.WorkArea.Height), menu.Height);
        Assert.Equal(Math.Min(840, SystemParameters.WorkArea.Width), menu.MinWidth);
        Assert.Equal(Math.Min(650, SystemParameters.WorkArea.Height), menu.MinHeight);
        var tabs = (TabControl)menu.FindName("MenuTabControl");
        Assert.Equal(new[] { "Уголок", "Покедекс", "Экспедиции", "Питомник", "Магазин" },
            tabs.Items.Cast<TabItem>().Select(System.Windows.Automation.AutomationProperties.GetName));
        menu.Navigate(MenuSection.Achievements);
        Assert.Equal(2, ((TabControl)menu.FindName("CornerTabs")).SelectedIndex);
        menu.SetCatchReady(3);
        var encounters = Assert.IsType<Button>(menu.FindName("EncountersButton"));
        Assert.Contains("3", encounters.Content.ToString());
        encounters.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Equal(1, ((TabControl)menu.FindName("CornerTabs")).SelectedIndex);
        menu.Close();
    });

    [Fact]
    public void ConfirmationBlocksBackgroundCancelsAndRetainsScreen() => WpfTestHost.Run(() =>
    {
        var menu = CreateMenu();
        menu.Navigate(MenuSection.Pokedex);
        var tabs = (TabControl)menu.FindName("MenuTabControl");
        var original = tabs.SelectedItem;
        var pending = menu.ConfirmAsync("Передать?", "Питомец покинет уголок.", "Передать");
        var overlay = Assert.IsAssignableFrom<FrameworkElement>(menu.FindName("ConfirmationHost"));
        Assert.False(((UIElement)menu.FindName("ShellContent")).IsEnabled);
        Assert.False(pending.IsCompleted);
        menu.Navigate(MenuSection.Nursery);
        Assert.Same(original, tabs.SelectedItem);
        Assert.Throws<InvalidOperationException>(() => { _ = menu.ConfirmAsync("Ещё?", "Текст", "Да"); });
        var cancel = (Button)overlay.FindName("CancelButton");
        cancel.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.False(CompletedResult(pending));
        Assert.Same(original, tabs.SelectedItem);
        Assert.True(((UIElement)menu.FindName("ShellContent")).IsEnabled);
        pending = menu.ConfirmAsync("Передать?", "Текст", "Передать");
        ((Button)overlay.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.True(CompletedResult(pending));
        pending = menu.ConfirmAsync("Передать?", "Текст", "Передать");
        menu.Close();
        Assert.False(CompletedResult(pending));
    });
}