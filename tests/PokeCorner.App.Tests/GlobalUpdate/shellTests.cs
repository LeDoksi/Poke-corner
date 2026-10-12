using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Tests.GlobalUpdate;

public class ShellTests
{
    [Fact]
    public void DirectTabSelectionReturnsFromSettingsToTheExactView() => WpfTestHost.Run(() =>
    {
        var menu = CreateMenu(new AppState());
        var main = (TabControl)menu.FindName("MenuTabControl");
        var nested = (TabControl)menu.FindName("CornerTabs");
        var settingsButton = (Button)menu.FindName("SettingsButton");
        foreach (var index in new[] { 1, 2, 0 })
        {
            main.SelectedIndex = index;
            if (index == 0) nested.SelectedIndex = 1;
            settingsButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.Equal(Visibility.Collapsed, main.Visibility);
            settingsButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.Equal(index, main.SelectedIndex);
            if (index == 0) Assert.Equal(1, nested.SelectedIndex);
        }
    });

    [Fact]
    public void RarityAndTypeLabelsMeetTextContrastOnTheirResolvedPaperSurface() => WpfTestHost.Run(() =>
    {
        foreach (var key in new[] { "RarityCommonBrush", "RarityUncommonBrush", "RarityRareBrush", "RarityLegendaryBrush" })
        {
            var text = new TextBlock { Text = "Редкость", Foreground = (Brush)Application.Current.Resources[key] };
            var card = new Border { Style = (Style)Application.Current.Resources["CardStyle"], Child = text };
            WpfTestHost.Layout(card, 180, 40);
            Assert.True(Contrast(((SolidColorBrush)text.Foreground).Color, ((SolidColorBrush)card.Background).Color) >= 4.5, key);
        }

        foreach (var type in new[] { "normal", "fire", "water", "electric", "grass", "ice", "fighting", "poison", "ground", "flying", "psychic", "bug", "rock", "ghost", "dragon", "dark", "steel", "fairy", "unknown" })
        {
            var presenter = new ContentPresenter
            {
                Content = TypeDisplay.ToBadge(type),
                ContentTemplate = (DataTemplate)Application.Current.Resources["TypeBadgeTemplate"]
            };
            WpfTestHost.Layout(presenter, 180, 40);
            var label = FindChild<TextBlock>(presenter);
            var paper = FindChild<Border>(presenter);
            Assert.True(Contrast(((SolidColorBrush)label.Foreground).Color, ((SolidColorBrush)paper.Background).Color) >= 4.5, type);
        }
    });

    [Fact]
    public void ResetActionUsesDistinctDangerInk() => WpfTestHost.Run(() =>
    {
        var view = new SettingsView(new SettingsViewModel(new AppSettings()), () => { });
        var button = (Button)view.FindName("ResetDataButton");
        Assert.Equal(((SolidColorBrush)Application.Current.Resources["DangerTextBrush"]).Color,
            ((SolidColorBrush)button.Foreground).Color);
    });

    [Fact]
    public void PaperCardsUseTheirBorderWithoutDarkHalos() => WpfTestHost.Run(() =>
    {
        foreach (var key in new[] { "CardStyle", "HeroCardStyle" })
        {
            var card = new Border { Style = (Style)Application.Current.Resources[key] };
            WpfTestHost.Layout(card, 180, 60);
            Assert.Null(card.Effect);
            Assert.True(card.BorderThickness.Left > 0);
        }
    });

    private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            if (VisualTreeHelper.GetChildrenCount(child) > 0)
            {
                try { return FindChild<T>(child); }
                catch (InvalidOperationException) { }
            }
        }
        throw new InvalidOperationException($"Missing {typeof(T).Name}");
    }

    private static double Contrast(Color a, Color b)
    {
        static double L(Color c)
        {
            static double C(byte value)
            {
                var n = value / 255.0;
                return n <= 0.04045 ? n / 12.92 : Math.Pow((n + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * C(c.R) + 0.7152 * C(c.G) + 0.0722 * C(c.B);
        }
        var x = L(a);
        var y = L(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

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

    [Fact]
    public void FinishReviewEvidenceRendersPopulatedPokedexAndResetArea() => WpfTestHost.Run(() =>
    {
        var output = Environment.GetEnvironmentVariable("POKE_SHELL_CAPTURE_DIR");
        Assert.False(string.IsNullOrWhiteSpace(output));
        Directory.CreateDirectory(output);

        var state = new AppState();
        var examples = new[]
        {
            (id: 19, name: "Rattata", rate: 200, type: "normal"),
            (id: 37, name: "Vulpix", rate: 100, type: "fire"),
            (id: 25, name: "Pikachu", rate: 45, type: "electric"),
            (id: 151, name: "Mew", rate: 3, type: "psychic")
        };
        foreach (var example in examples)
            state.Collection.Add(new PokemonIndividual
            {
                SpeciesId = example.id,
                EvolutionChainId = example.id,
                ChainLength = 1,
                Nature = Nature.Hardy,
                IsShiny = false,
                CaughtAtUtc = DateTimeOffset.UtcNow
            });
        var chains = examples.Select(example => new SpeciesEvolutionChain(
            example.id, example.id, example.rate, 1, new[] { example.id },
            new[] { example.name }, new IReadOnlyList<string>[] { new[] { example.type } })).ToArray();
        var vm = new CollectionViewModel(state.Collection, state.CareStateByIndividualId,
            new CareModeService(), chains, state.PendingEggs);
        var collection = new CollectionView(vm, state,
            new SpriteCache(Path.Combine(Path.GetTempPath(), "pokecorner-shell-evidence", Guid.NewGuid().ToString("N"))),
            () => { }, () => { }, () => { }, () => null, _ => true, () => true, () => true);
        var menu = new AppMenuWindow(collection, new SettingsView(new SettingsViewModel(state.Settings), () => { }),
            new ShopView(state, new ShopService(), _ => true, _ => true, () => { }),
            new CatchView(state, () => null, _ => Task.FromResult<byte[]?>(null), () => 0, () => { }, id => $"Pokemon #{id}"));

        menu.Navigate(MenuSection.Pokedex);
        var root = (FrameworkElement)menu.Content;
        WpfTestHost.Layout(root, 820, 660);
        var collectionTabs = FindChild<TabControl>(collection);
        collectionTabs.SelectedIndex = 1;
        WpfTestHost.Layout(root, 820, 660);
        var grid = (ItemsControl)collection.FindName("PokedexGrid");
        Assert.Equal(4, grid.Items.Count);
        var pokedexScroll = (ScrollViewer)((TabItem)collectionTabs.Items[1]).Content;
        pokedexScroll.ScrollToVerticalOffset(160);
        root.UpdateLayout();
        Capture(root, 820, 660, 1.5, Path.Combine(output, "pokedex-populated-150.png"));

        menu.Navigate(MenuSection.Settings);
        WpfTestHost.Layout(root, 640, 480);
        var settingsView = (SettingsView)((ContentControl)menu.FindName("SettingsContent")).Content;
        var settingsScroll = (ScrollViewer)settingsView.Content;
        settingsScroll.ScrollToEnd();
        root.UpdateLayout();
        Assert.True(settingsScroll.VerticalOffset > 0);
        Capture(root, 640, 480, 2.0, Path.Combine(output, "settings-reset-compact-200.png"));
    });

    private static void Capture(FrameworkElement root, int width, int height, double scale, string path)
    {
        var bitmap = new RenderTargetBitmap((int)(width * scale), (int)(height * scale),
            96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(root);
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }

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
