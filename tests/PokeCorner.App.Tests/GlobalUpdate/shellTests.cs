using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Tests.GlobalUpdate;

public class ShellTests
{
    [Fact]
    public void FiveSectionsAndSettingsKeepCatchAndSoundControlsReachable() => WpfTestHost.Run(() =>
    {
        var state = new AppState();
        var settings = state.Settings;
        var menu = CreateMenu(state);
        var tabs = (TabControl)menu.FindName("MenuTabControl");
        Assert.Equal(5, tabs.Items.Count);
        foreach (var section in new[] { MenuSection.Corner, MenuSection.Pokedex, MenuSection.Shop, MenuSection.Expeditions, MenuSection.Nursery })
        {
            menu.Navigate(section);
            Assert.Equal((int)section, tabs.SelectedIndex);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName((TabItem)tabs.SelectedItem)));
        }

        menu.Navigate(MenuSection.Catch);
        Assert.Equal(0, tabs.SelectedIndex);
        Assert.Equal(1, ((TabControl)menu.FindName("CornerTabs")).SelectedIndex);
        menu.SetCatchReady(2);
        Assert.Equal(Visibility.Visible, ((FrameworkElement)menu.FindName("CatchBadge")).Visibility);

        menu.Navigate(MenuSection.Shop);
        WpfTestHost.Layout((FrameworkElement)menu.Content, 820, 660);
        var shop = (ShopView)((TabItem)tabs.SelectedItem).Content;
        var productName = (TextBlock)shop.FindName("TreatPackNameText");
        Assert.Equal(((SolidColorBrush)Application.Current.Resources["PrimaryTextBrush"]).Color,
            ((SolidColorBrush)productName.Foreground).Color);

        menu.Navigate(MenuSection.Settings);
        Assert.Equal(Visibility.Collapsed, tabs.Visibility);
        var settingsView = (SettingsView)((ContentControl)menu.FindName("SettingsContent")).Content;
        Assert.Equal(30, ((Slider)settingsView.FindName("EffectsSoundVolumeSlider")).Value);
        ((Slider)settingsView.FindName("EffectsSoundVolumeSlider")).Value = 0;
        Assert.Equal(0, settings.EffectsSoundVolume);
        Assert.Equal(70, settings.CrySoundVolume);
        using var inputSource = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Shell key test")
        {
            WindowStyle = 0,
            Width = 1,
            Height = 1
        });
        var escape = new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, 0, Key.Escape)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };
        menu.RaiseEvent(escape);
        Assert.True(escape.Handled);
        Assert.Equal(Visibility.Visible, tabs.Visibility);
        Assert.Same((TabItem)tabs.Items[2], tabs.SelectedItem);
    });

    [Fact]
    public void BundledNunitoResolvesToAnActualTypeface() => WpfTestHost.Run(() =>
    {
        var menu = CreateMenu(new AppState());
        var typeface = new Typeface(menu.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        Assert.True(typeface.TryGetGlyphTypeface(out var glyph));
        Assert.Contains("nunito.ttf", glyph.FontUri.ToString(), StringComparison.OrdinalIgnoreCase);
    });

    [Fact]
    public void NativeShellRendersAtCompactAndRegularDpi() => WpfTestHost.Run(() =>
    {
        var output = Environment.GetEnvironmentVariable("POKE_SHELL_CAPTURE_DIR");
        foreach (var (width, height, scale, section, name) in new[]
        {
            (820, 660, 1.5, MenuSection.Corner, "regular-150"),
            (640, 480, 2.0, MenuSection.Corner, "compact-200"),
            (640, 480, 2.0, MenuSection.Settings, "settings-compact-200"),
            (820, 660, 1.5, MenuSection.Pokedex, "pokedex-regular-150"),
            (820, 660, 1.5, MenuSection.Shop, "shop-regular-150"),
            (820, 660, 1.5, MenuSection.Catch, "catch-regular-150"),
            (640, 480, 2.0, MenuSection.Expeditions, "expeditions-compact-200"),
            (640, 480, 2.0, MenuSection.Nursery, "nursery-compact-200")
        })
        {
            var menu = CreateMenu(new AppState());
            menu.Navigate(section);
            var root = (FrameworkElement)menu.Content;
            WpfTestHost.Layout(root, width, height);
            Assert.True(root.ActualWidth >= width);
            Assert.True(root.ActualHeight >= height);
            var bitmap = new RenderTargetBitmap((int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(root);
            Assert.True(bitmap.PixelWidth > 0);
            if (output is null) continue;
            Directory.CreateDirectory(output);
            using var stream = File.Create(Path.Combine(output, name + ".png"));
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bitmap));
            png.Save(stream);
        }
    });

    private static AppMenuWindow CreateMenu(AppState state)
    {
        var settings = state.Settings;
        var vm = new CollectionViewModel(state.Collection, state.CareStateByIndividualId, new CareModeService(),
            Array.Empty<SpeciesEvolutionChain>(), state.PendingEggs);
        var collection = new CollectionView(vm, state, new SpriteCache(Path.Combine(Path.GetTempPath(), "pokecorner-shell-test", Guid.NewGuid().ToString("N"))),
            () => { }, () => { }, () => { }, () => null, _ => true, () => true, () => true);
        return new AppMenuWindow(collection, new SettingsView(new SettingsViewModel(settings), () => { }),
            new ShopView(state, new ShopService(), _ => true, _ => true, () => { }),
            new CatchView(state, () => null, _ => Task.FromResult<byte[]?>(null), () => 0, () => { }, id => $"Pokemon #{id}"));
    }
}
