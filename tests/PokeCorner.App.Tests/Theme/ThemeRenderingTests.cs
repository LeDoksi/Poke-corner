using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using PokeCorner.App.Onboarding;
using PokeCorner.App.Popup;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;
using Xunit;

namespace PokeCorner.App.Tests.Theme;

/// Verifies the redesign renders, not just compiles. A clean build proves nothing about
/// XAML: a missing StaticResource key or a ProgressBar template that breaks the
/// PART_Track/PART_Indicator contract compiles fine and only fails when a user opens the
/// window. These tests construct the real views on a real WPF Application and run a full
/// layout pass over them.
public class ThemeRenderingTests
{
    [Theory]
    [InlineData("WindowBackgroundBrush")]
    [InlineData("SurfaceBackgroundBrush")]
    [InlineData("SurfaceHoverBrush")]
    [InlineData("CardBackgroundBrush")]
    [InlineData("CardHoverBackgroundBrush")]
    [InlineData("CardBorderBrush")]
    [InlineData("AccentRedBrush")]
    [InlineData("AccentRedHoverBrush")]
    [InlineData("AccentGoldBrush")]
    [InlineData("AccentGoldHoverBrush")]
    [InlineData("AccentYellowBrush")]
    [InlineData("PrimaryTextBrush")]
    [InlineData("SecondaryTextBrush")]
    [InlineData("MutedTextBrush")]
    [InlineData("DisabledSurfaceBrush")]
    [InlineData("RarityCommonBrush")]
    [InlineData("RarityUncommonBrush")]
    [InlineData("RarityRareBrush")]
    [InlineData("RarityLegendaryBrush")]
    [InlineData("PrimaryButtonBrush")]
    [InlineData("ProgressGoldBrush")]
    [InlineData("ProgressRoseBrush")]
    [InlineData("ProgressBlueBrush")]
    [InlineData("ProgressGreenBrush")]
    [InlineData("SpotlightBrush")]
    [InlineData("HeroCardBrush")]
    [InlineData("AppWindowStyle")]
    [InlineData("CardStyle")]
    [InlineData("HeroCardStyle")]
    [InlineData("WellStyle")]
    [InlineData("PillStyle")]
    [InlineData("SecondaryButtonStyle")]
    [InlineData("CatchProgressBarStyle")]
    [InlineData("EvolutionProgressBarStyle")]
    [InlineData("TreatProgressBarStyle")]
    [InlineData("GoldProgressBarStyle")]
    [InlineData("SlimProgressBarStyle")]
    [InlineData("MiniProgressBarStyle")]
    [InlineData("FilterPillStyle")]
    [InlineData("FavoriteToggleStyle")]
    [InlineData("ScreenTitleTextStyle")]
    [InlineData("SectionHeaderTextStyle")]
    [InlineData("StatValueTextStyle")]
    [InlineData("StatLabelTextStyle")]
    [InlineData("MetaTextStyle")]
    [InlineData("CaptionTextStyle")]
    public void Theme_ExposesDesignToken(string resourceKey) =>
        WpfTestHost.Run(() => Assert.NotNull(Application.Current.Resources[resourceKey]));

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.25, 50.0)]
    [InlineData(0.5, 100.0)]
    [InlineData(1.0, 200.0)]
    public void ProgressBarTemplate_FillsIndicatorProportionally(double value, double expectedIndicatorWidth) =>
        WpfTestHost.Run(() =>
        {
            var bar = new ProgressBar
            {
                // The unkeyed theme style, assigned explicitly: a ProgressBar built in code
                // and never attached to a tree doesn't run the implicit-style lookup that the
                // XAML parser performs, so fetching it by type key is how this test exercises
                // the same style ProgressPanelWindow's unstyled bars get at runtime.
                Style = (Style)Application.Current.Resources[typeof(ProgressBar)],
                Minimum = 0,
                Maximum = 1,
                Value = value,
                Width = 200,
                Height = 10
            };
            WpfTestHost.Layout(bar, 200, 10);

            var indicator = (FrameworkElement)bar.Template.FindName("PART_Indicator", bar);
            Assert.Equal(expectedIndicatorWidth, indicator.ActualWidth, precision: 1);
        });

    [Fact]
    public void SemanticProgressBarStyles_AllHonorTheIndicatorContract() =>
        WpfTestHost.Run(() =>
        {
            var styleKeys = new[]
            {
                "CatchProgressBarStyle", "EvolutionProgressBarStyle", "TreatProgressBarStyle",
                "GoldProgressBarStyle", "SlimProgressBarStyle"
            };

            foreach (var key in styleKeys)
            {
                var bar = new ProgressBar
                {
                    Style = (Style)Application.Current.Resources[key],
                    Minimum = 0,
                    Maximum = 1,
                    Value = 0.5,
                    Width = 200
                };
                WpfTestHost.Layout(bar, 200, 20);

                var indicator = (FrameworkElement)bar.Template.FindName("PART_Indicator", bar);
                Assert.True(Math.Abs(indicator.ActualWidth - 100.0) < 1.0,
                    $"{key} rendered a {indicator.ActualWidth}px indicator inside a 200px track; expected ~100px.");
            }
        });

    [Fact]
    public void CollectionView_BothTabs_LayOutWithRealData() =>
        WpfTestHost.Run(() =>
        {
            var view = BuildCollectionView();
            WpfTestHost.Layout(view);

            var tabs = (TabControl)view.Content;
            Assert.Equal(2, tabs.Items.Count);

            // Only the selected tab's content is realized, so the Pokédex card template is
            // exercised by switching tabs and laying out again.
            tabs.SelectedIndex = 1;
            WpfTestHost.Layout(view);
        });

    [Fact]
    public void CollectionView_RosterCards_ShowDexNumberNatureAndCatchDate() =>
        WpfTestHost.Run(() =>
        {
            var view = BuildCollectionView();
            WpfTestHost.Layout(view);

            var roster = (ItemsControl)view.FindName("EntriesList");
            var card = Assert.IsType<CollectionCard>(Assert.Single(roster.ItemsSource.Cast<object>()));

            Assert.Equal("#004", card.DexNumber);
            Assert.Contains("Hardy", card.MetaLine);
            Assert.Contains(DateTimeOffset.UtcNow.ToLocalTime().ToString("dd.MM.yyyy"), card.MetaLine);
            Assert.NotNull(card.RarityBrush);
        });

    // ---------------------------------------------------------------- user-feedback wave

    /// Item 1: the home tab is "what's active right now" only. Eggs and the roster moved to
    /// the Pokédex tab, and this asserts on the actual visual ancestry rather than trusting
    /// that the XAML block was pasted under the right TabItem.
    [Fact]
    public void CollectionView_EggsAndRosterLiveOnThePokedexTab_NotTheHomeTab() =>
        WpfTestHost.Run(() =>
        {
            var view = BuildCollectionView();
            WpfTestHost.Layout(view);

            var tabs = (TabControl)view.Content;
            var homeTab = (TabItem)tabs.Items[0];
            var pokedexTab = (TabItem)tabs.Items[1];

            foreach (var name in new[] { "PendingEggsList", "EggsEmptyText", "EntriesList", "RosterCountText" })
            {
                var element = (FrameworkElement)view.FindName(name)!;
                Assert.True(IsLogicalDescendantOf(element, pokedexTab), $"{name} should sit under the Pokédex tab.");
                Assert.False(IsLogicalDescendantOf(element, homeTab), $"{name} should no longer sit under the home tab.");
            }

            // The hero and progress cards stayed put.
            foreach (var name in new[] { "ActiveSpriteImage", "CatchProgressBar", "TreatHintText" })
            {
                Assert.True(IsLogicalDescendantOf((FrameworkElement)view.FindName(name)!, homeTab),
                    $"{name} should still sit under the home tab.");
            }
        });

    /// Item 2a: selecting a rarity pill narrows the species grid, and "Все" restores it.
    [Fact]
    public void CollectionView_RarityFilter_NarrowsThePokedexGridAndAllRestoresIt() =>
        WpfTestHost.Run(() =>
        {
            // Two species in two different rarity tiers: capture rate 255 => Common,
            // capture rate 45 => Rare.
            var appState = new AppState();
            appState.Collection.Add(NewIndividual(speciesId: 4, chainId: 4));
            appState.Collection.Add(NewIndividual(speciesId: 16, chainId: 16));
            var chains = new[]
            {
                new SpeciesEvolutionChain(4, 4, 45, 3, new[] { 4, 5, 6 }),
                new SpeciesEvolutionChain(16, 16, 255, 3, new[] { 16, 17, 18 })
            };
            var view = BuildCollectionView(appState, chains);
            WpfTestHost.Layout(view);

            var grid = (ItemsControl)view.FindName("PokedexGrid")!;
            Assert.Equal(2, grid.ItemsSource.Cast<object>().Count());

            ((RadioButton)view.FindName("RarityFilterRare")!).IsChecked = true;
            var rareOnly = grid.ItemsSource.Cast<PokedexCard>().ToList();
            Assert.Equal(4, Assert.Single(rareOnly).SpeciesId);

            ((RadioButton)view.FindName("RarityFilterCommon")!).IsChecked = true;
            Assert.Equal(16, Assert.Single(grid.ItemsSource.Cast<PokedexCard>()).SpeciesId);

            // A tier with nothing in it empties the grid and explains why.
            ((RadioButton)view.FindName("RarityFilterLegendary")!).IsChecked = true;
            Assert.Empty(grid.ItemsSource.Cast<PokedexCard>());
            var emptyText = (TextBlock)view.FindName("PokedexEmptyText")!;
            Assert.Equal(Visibility.Visible, emptyText.Visibility);
            Assert.Contains("редкости", emptyText.Text);

            ((RadioButton)view.FindName("RarityFilterAll")!).IsChecked = true;
            Assert.Equal(2, grid.ItemsSource.Cast<object>().Count());

            // The headline count always reports total completion, never the filtered subset.
            Assert.Contains("2", ((TextBlock)view.FindName("PokedexCountText")!).Text);
        });

    /// Item 2a, second half: the filter must survive the once-a-second refresh tick, which
    /// rebuilds the grid from scratch. Refresh() is what that timer calls.
    [Fact]
    public void CollectionView_RarityFilter_SurvivesARefreshTick() =>
        WpfTestHost.Run(() =>
        {
            var appState = new AppState();
            appState.Collection.Add(NewIndividual(speciesId: 4, chainId: 4));
            appState.Collection.Add(NewIndividual(speciesId: 16, chainId: 16));
            var chains = new[]
            {
                new SpeciesEvolutionChain(4, 4, 45, 3, new[] { 4, 5, 6 }),
                new SpeciesEvolutionChain(16, 16, 255, 3, new[] { 16, 17, 18 })
            };
            var view = BuildCollectionView(appState, chains);
            WpfTestHost.Layout(view);

            ((RadioButton)view.FindName("RarityFilterRare")!).IsChecked = true;
            view.Refresh();

            var grid = (ItemsControl)view.FindName("PokedexGrid")!;
            Assert.Equal(4, Assert.Single(grid.ItemsSource.Cast<PokedexCard>()).SpeciesId);
        });

    /// Item 2b: the pinned individual is first in the roster and its star renders filled gold.
    [Fact]
    public void CollectionView_FavoriteIndividual_SortsFirstAndRendersAGoldStar() =>
        WpfTestHost.Run(() =>
        {
            var appState = new AppState();
            var plain = NewIndividual(speciesId: 4, chainId: 4);
            var pinned = NewIndividual(speciesId: 16, chainId: 16);
            appState.Collection.Add(plain);
            appState.Collection.Add(pinned);
            pinned.IsFavorite = true;

            var view = BuildCollectionView(appState, new[]
            {
                new SpeciesEvolutionChain(4, 4, 45, 3, new[] { 4, 5, 6 }),
                new SpeciesEvolutionChain(16, 16, 255, 3, new[] { 16, 17, 18 })
            });
            WpfTestHost.Layout(view);

            var roster = (ItemsControl)view.FindName("EntriesList")!;
            var cards = roster.ItemsSource.Cast<CollectionCard>().ToList();

            Assert.Equal(pinned.Id, cards[0].IndividualId);
            Assert.True(cards[0].IsFavorite);
            Assert.Equal(Visibility.Visible, cards[0].FavoriteBadgeVisibility);
            Assert.Same(Application.Current.Resources["AccentGoldBrush"], cards[0].FavoriteFill);

            // The unpinned card's star is hollow with a muted outline, not gold.
            Assert.False(cards[1].IsFavorite);
            Assert.Equal(Visibility.Collapsed, cards[1].FavoriteBadgeVisibility);
            Assert.Same(System.Windows.Media.Brushes.Transparent, cards[1].FavoriteFill);
            Assert.Same(Application.Current.Resources["MutedTextBrush"], cards[1].FavoriteStroke);
        });

    /// Item 3: the compact bar must actually fill proportionally AND must not stack a gloss
    /// overlay inside its indicator — at 8px that overlay is what made the three bars on the
    /// desktop panel blur into one dark blob.
    [Fact]
    public void MiniProgressBarStyle_FillsProportionallyAndCarriesNoGlossOverlay() =>
        WpfTestHost.Run(() =>
        {
            var bar = new ProgressBar
            {
                Style = (Style)Application.Current.Resources["MiniProgressBarStyle"],
                Minimum = 0,
                Maximum = 1,
                Value = 0.5,
                Width = 100
            };
            WpfTestHost.Layout(bar, 100, 20);

            var indicator = (Border)bar.Template.FindName("PART_Indicator", bar);
            Assert.Equal(50.0, indicator.ActualWidth, precision: 1);
            Assert.Null(indicator.Child); // the shared template puts a 3px gloss Border here

            var track = (Border)bar.Template.FindName("PART_Track", bar);
            // BorderThickness is template-bound now, so a borderless bar really is borderless.
            Assert.Equal(new Thickness(0), track.BorderThickness);
            Assert.Equal(8.0, bar.ActualHeight, precision: 1);
        });

    /// Item 3: the panel itself. It used to size its width from content (which has no
    /// intrinsic width), collapsing to a sliver; it now has a real width and three bars that
    /// each render their own distinct color.
    [Fact]
    public void ProgressPanelWindow_HasRealWidthAndThreeDistinctlyColoredBars() =>
        WpfTestHost.Run(() =>
        {
            var panel = new PokeCorner.App.Overlay.ProgressPanelWindow();
            panel.UpdateProgress(new ProgressInfo(0.5, 0.25, 0.75));

            // Measured at the window's real width, which is the whole point: the bars have no
            // intrinsic width and only become readable because the window supplies one.
            var root = (FrameworkElement)panel.Content;
            root.Measure(new Size(panel.Width, double.PositiveInfinity));
            root.Arrange(new Rect(0, 0, panel.Width, root.DesiredSize.Height));
            root.UpdateLayout();

            Assert.True(panel.Width >= 100, $"panel is only {panel.Width}px wide");
            Assert.Equal(SizeToContent.Height, panel.SizeToContent);

            var bars = new[] { "CatchBar", "EvolutionBar", "TreatBar" }
                .Select(name => (ProgressBar)panel.FindName(name)!)
                .ToList();

            Assert.Equal(new[] { 0.5, 0.25, 0.75 }, bars.Select(b => b.Value));

            // Three different fill colors, each at least 8px tall and spanning most of
            // the panel's width — i.e. three readable bars, not one dark pill.
            var colors = bars.Select(b => ((SolidColorBrush)b.Foreground).Color).ToList();
            Assert.Equal(3, colors.Distinct().Count());
            foreach (var bar in bars)
            {
                Assert.True(bar.ActualHeight >= 8, $"bar is only {bar.ActualHeight}px tall");
                Assert.True(bar.ActualWidth >= 80, $"bar is only {bar.ActualWidth}px wide");
            }
        });

    /// Item 5: the closed ComboBox display. WPF's stock ButtonBase style sets a BLACK
    /// Foreground on the ToggleButton that renders the current selection, and a style setter
    /// outranks the inherited value from the ComboBox — so the selected mode was drawn in
    /// near-black on the dark navy card. This asserts the light foreground reaches it.
    [Fact]
    public void SettingsView_CareModeCombo_ClosedBoxTextIsLegibleNotSystemBlack() =>
        WpfTestHost.Run(() =>
        {
            var view = new SettingsView(new SettingsViewModel(new AppState().Settings), () => { });
            WpfTestHost.Layout(view);

            var combo = (ComboBox)view.FindName("CareModeCombo")!;
            var toggle = (ToggleButton)combo.Template.FindName("Toggle", combo);

            var expected = (SolidColorBrush)Application.Current.Resources["PrimaryTextBrush"];
            var actual = Assert.IsType<SolidColorBrush>(toggle.Foreground);
            Assert.Equal(expected.Color, actual.Color);
            Assert.NotEqual(Colors.Black, actual.Color);
            Assert.NotEqual(SystemColors.ControlTextColor, actual.Color);

            // And it must contrast with what it is drawn on: the closed box's own surface.
            var closedBox = (Border)toggle.Template.FindName("ClosedBox", toggle);
            var background = Assert.IsType<SolidColorBrush>(closedBox.Background);
            Assert.True(RelativeLuminanceGap(actual.Color, background.Color) > 0.4,
                "the selection text does not contrast with the closed box behind it");
        });

    /// Item 5, dropdown half: an item that is selected but not hovered must stay readable.
    [Fact]
    public void ComboBoxItem_SelectedState_KeepsAReadableForeground() =>
        WpfTestHost.Run(() =>
        {
            var item = new ComboBoxItem { Content = "Hardcore", IsSelected = true };
            var host = new ComboBox();
            host.Items.Add(item);
            WpfTestHost.Layout(host, 200, 40);

            var foreground = Assert.IsType<SolidColorBrush>(item.Foreground);
            Assert.NotEqual(Colors.Black, foreground.Color);
            Assert.NotEqual(SystemColors.ControlTextColor, foreground.Color);
        });

    /// Item 6: onboarding shows one progress dot per step, with the current one highlighted.
    [Fact]
    public void OnboardingWindow_ShowsOneHighlightedDotPerStepAndNoFixedHeightBody() =>
        WpfTestHost.Run(() =>
        {
            var window = new OnboardingWindow();
            var root = (FrameworkElement)window.Content;
            WpfTestHost.Layout(root, window.Width, window.Height);

            var dots = ((System.Windows.Controls.Panel)window.FindName("StepDotsPanel")!).Children.Cast<Border>().ToList();
            Assert.True(dots.Count > 1, "expected one progress dot per onboarding step");

            var accent = (SolidColorBrush)Application.Current.Resources["AccentRedBrush"];
            var highlighted = dots.Where(d => ((SolidColorBrush)d.Background).Color == accent.Color).ToList();
            Assert.Single(highlighted);
            Assert.Same(dots[0], highlighted[0]); // starts on step 1

            Assert.Contains("1", ((TextBlock)window.FindName("StepCounterText")!).Text);

            // The body no longer carries the rigid Height="110" that produced the awkward
            // whitespace; it sizes to its content instead.
            var body = (TextBlock)window.FindName("BodyText")!;
            Assert.True(double.IsNaN(body.Height), "the onboarding body should auto-size, not use a fixed Height");
            Assert.False(string.IsNullOrWhiteSpace(body.Text));
        });

    /// Item 7: the first-run starter picker rendered as a WHITE window with near-white title
    /// text on it. Root cause was not this window at all — the theme's window chrome was an
    /// unkeyed `Style TargetType="Window"`, and WPF only matches implicit styles against an
    /// element's EXACT runtime type, so no Window SUBCLASS in the app ever picked it up.
    /// AppMenuWindow merely hid it by painting its whole client area.
    ///
    /// Every window that opts into AppWindowStyle is checked here, so re-introducing the
    /// implicit-style assumption fails loudly instead of shipping another white window.
    [Fact]
    public void EveryChromeWindow_PaintsTheDarkThemeBackgroundNotSystemWhite() =>
        WpfTestHost.Run(() =>
        {
            var chains = new[]
            {
                new SpeciesEvolutionChain(10001, 10, 255, 3, new[] { 10, 11, 12 }),
                new SpeciesEvolutionChain(10002, 20, 255, 3, new[] { 20, 21, 22 }),
                new SpeciesEvolutionChain(10003, 30, 255, 3, new[] { 30, 31, 32 })
            };
            var appState = new AppState();

            var windows = new Window[]
            {
                new StarterSelectionWindow(chains, _ => Task.FromResult<byte[]?>(null)),
                new OnboardingWindow(),
                new AppMenuWindow(
                    BuildCollectionView(),
                    new SettingsView(new SettingsViewModel(appState.Settings), () => { }),
                    new ShopView(appState, new ShopService(), _ => true, _ => true, () => { }),
                    new CatchView(appState, () => null, _ => Task.FromResult<byte[]?>(null), () => false, () => { }))
            };

            var expected = (SolidColorBrush)Application.Current.Resources["WindowBackgroundBrush"];
            foreach (var window in windows)
            {
                var actual = Assert.IsType<SolidColorBrush>(window.Background);
                Assert.True(expected.Color == actual.Color,
                    $"{window.GetType().Name} painted {actual.Color} instead of the theme's {expected.Color}");
                Assert.NotEqual(Colors.White, actual.Color);

                // Foreground comes from the same style; a white window with white text was
                // exactly how the starter picker's title went invisible.
                var foreground = Assert.IsType<SolidColorBrush>(window.Foreground);
                Assert.True(RelativeLuminanceGap(foreground.Color, actual.Color) > 0.4,
                    $"{window.GetType().Name}'s text does not contrast with its own background");
            }
        });

    /// The starter picker specifically: it uses neither TabControl nor ComboBox (the two
    /// controls whose stock chrome caused the earlier white-window bug), and must lay out.
    [Fact]
    public void StarterSelectionWindow_LaysOutWithNoLightChromeControls() =>
        WpfTestHost.Run(() =>
        {
            var chains = new[]
            {
                new SpeciesEvolutionChain(10001, 10, 255, 3, new[] { 10, 11, 12 }),
                new SpeciesEvolutionChain(10002, 20, 255, 3, new[] { 20, 21, 22 }),
                new SpeciesEvolutionChain(10003, 30, 255, 3, new[] { 30, 31, 32 })
            };
            var window = new StarterSelectionWindow(chains, _ => Task.FromResult<byte[]?>(null));
            var root = (FrameworkElement)window.Content;
            WpfTestHost.Layout(root, window.Width, window.Height);

            Assert.Empty(Descendants(root).OfType<TabControl>());
            Assert.Empty(Descendants(root).OfType<ComboBox>());
        });

    /// Item 4: the pet-size slider had no live preview — it wrote the setting and the sprite
    /// only resized whenever the app's next 5-second reconcile tick happened to run. The view
    /// now invokes an apply-callback synchronously on EVERY value change, which is what makes
    /// dragging the slider resize the real desktop sprite as you drag.
    [Fact]
    public void SettingsView_PetSizeSlider_AppliesTheNewSizeOnEveryTickNotJustAtTheEnd() =>
        WpfTestHost.Run(() =>
        {
            var settings = new AppState().Settings;
            var appliedSizes = new List<int>();
            var view = new SettingsView(new SettingsViewModel(settings), () => appliedSizes.Add(settings.PetSizePixels));
            WpfTestHost.Layout(view);

            var slider = (Slider)view.FindName("PetSizeSlider")!;

            // Loading the initial value must NOT fire the callback (it would fight the
            // overlay's own position on every popup open).
            Assert.Empty(appliedSizes);

            // Three intermediate values, as a drag produces.
            slider.Value = 100;
            slider.Value = 220;
            slider.Value = 340;

            Assert.Equal(new[] { 100, 220, 340 }, appliedSizes);
            Assert.Equal(340, settings.PetSizePixels);
            Assert.Equal("340 px", ((TextBlock)view.FindName("PetSizeValueText")!).Text);
        });

    [Fact]
    public void SettingsView_PetSizeSlider_ClampsToTheSupportedRange() =>
        WpfTestHost.Run(() =>
        {
            var settings = new AppState().Settings;
            var view = new SettingsView(new SettingsViewModel(settings), () => { });
            WpfTestHost.Layout(view);

            var slider = (Slider)view.FindName("PetSizeSlider")!;
            Assert.Equal(SettingsViewModel.MinPetSizePixels, slider.Minimum);
            Assert.Equal(SettingsViewModel.MaxPetSizePixels, slider.Maximum);
        });

    /// Item 8: the hero sprite in the menu used to receive only frame 0 of the active
    /// individual's sprite, so it sat frozen while the desktop pet animated. It now takes the
    /// whole frame list and drives it with the overlay's own animator.
    [Fact]
    public void CollectionView_HeroSprite_MultiFrameSpecies_ActuallyCyclesFrames() =>
        WpfTestHost.Run(() =>
        {
            var frames = new[] { SolidPng(Color.FromRgb(255, 0, 0)), SolidPng(Color.FromRgb(0, 0, 255)) };
            var view = BuildCollectionView(new AppState(), Array.Empty<SpeciesEvolutionChain>(), () => frames);
            WpfTestHost.Layout(view);

            var image = (System.Windows.Controls.Image)view.FindName("ActiveSpriteImage")!;
            Assert.NotNull(image.Source); // frame 0 is assigned up front, never left blank

            // FrameSequencer advances every 150ms, so pumping past that must land on a
            // different frame. Collect what the animator actually renders.
            var seen = new HashSet<ImageSource>();
            var deadline = DateTime.UtcNow.AddMilliseconds(700);
            while (DateTime.UtcNow < deadline)
            {
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                if (image.Source is not null) seen.Add(image.Source);
                Thread.Sleep(20);
            }

            Assert.True(seen.Count >= 2, $"the hero sprite never changed frame (saw {seen.Count} distinct frames)");

            // The placeholder pokéball must be out of the way once real frames exist.
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)view.FindName("ActiveSpritePlaceholder")!).Visibility);
        });

    /// A species with no animated sprite is still shown — just statically, with no timer.
    [Fact]
    public void CollectionView_HeroSprite_SingleFrameSpecies_RendersStaticallyWithoutBlanking() =>
        WpfTestHost.Run(() =>
        {
            var frames = new[] { SolidPng(Color.FromRgb(0, 255, 0)) };
            var view = BuildCollectionView(new AppState(), Array.Empty<SpeciesEvolutionChain>(), () => frames);
            WpfTestHost.Layout(view);

            var image = (System.Windows.Controls.Image)view.FindName("ActiveSpriteImage")!;
            var first = image.Source;
            Assert.NotNull(first);

            view.Refresh(); // the once-a-second tick must not tear the sprite down
            Assert.Same(first, image.Source);
        });

    /// No cached sprite yet: placeholder shows and nothing throws.
    [Fact]
    public void CollectionView_HeroSprite_NoFramesYet_FallsBackToThePlaceholder() =>
        WpfTestHost.Run(() =>
        {
            var view = BuildCollectionView(new AppState(), Array.Empty<SpeciesEvolutionChain>(), () => null);
            WpfTestHost.Layout(view);

            Assert.Null(((System.Windows.Controls.Image)view.FindName("ActiveSpriteImage")!).Source);
            Assert.Equal(Visibility.Visible, ((FrameworkElement)view.FindName("ActiveSpritePlaceholder")!).Visibility);
        });

    private static byte[] SolidPng(Color color)
    {
        var bitmap = new System.Windows.Media.Imaging.WriteableBitmap(8, 8, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new byte[8 * 8 * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = color.B;
            pixels[i + 1] = color.G;
            pixels[i + 2] = color.R;
            pixels[i + 3] = 255;
        }
        bitmap.WritePixels(new Int32Rect(0, 0, 8, 8), pixels, 8 * 4, 0);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static bool IsLogicalDescendantOf(DependencyObject element, DependencyObject ancestor)
    {
        for (var current = LogicalTreeHelper.GetParent(element); current is not null; current = LogicalTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, ancestor)) return true;
        }
        return false;
    }

    /// Crude perceptual gap between two colors — enough to catch "text is the same shade as
    /// the panel behind it" without pulling in a full WCAG contrast implementation.
    private static double RelativeLuminanceGap(Color a, Color b)
    {
        static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
        return Math.Abs(Luminance(a) - Luminance(b));
    }

    private static PokemonIndividual NewIndividual(int speciesId, int chainId) => new()
    {
        SpeciesId = speciesId,
        EvolutionChainId = chainId,
        ChainLength = 3,
        Nature = Nature.Hardy,
        IsShiny = false,
        CaughtAtUtc = DateTimeOffset.UtcNow
    };

    private static CollectionView BuildCollectionView(
        AppState appState,
        IReadOnlyList<SpeciesEvolutionChain> chains,
        Func<IReadOnlyList<byte[]>?>? getActiveSpriteFrames = null)
    {
        if (appState.Collection.Individuals.Count == 0)
        {
            appState.Collection.Add(NewIndividual(speciesId: 4, chainId: 4));
        }

        var viewModel = new CollectionViewModel(appState.Collection, appState.CareStateByIndividualId,
            new CareModeService(), CareMode.Chill, chains, appState.PendingEggs);
        var spriteCacheDirectory = Path.Combine(Path.GetTempPath(), "pokecorner-theme-tests", Guid.NewGuid().ToString("N"));
        return new CollectionView(viewModel, appState, new SpriteCache(spriteCacheDirectory),
            () => { }, () => { }, () => { }, getActiveSpriteFrames ?? (() => null), _ => true);
    }

    [Fact]
    public void ShopView_LaysOutEveryItemCard() =>
        WpfTestHost.Run(() =>
        {
            var appState = new AppState();
            var view = new ShopView(appState, new ShopService(), _ => true, _ => true, () => { });
            WpfTestHost.Layout(view);

            Assert.Equal("20", ((TextBlock)view.FindName("TreatPackPriceText")).Text);
            Assert.Equal("300", ((TextBlock)view.FindName("EggLegendaryPriceText")).Text);
            Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("MessagePanel")).Visibility);
        });

    [Fact]
    public void ShopView_UnavailableTier_DimsTheWholeCardNotJustTheButton() =>
        WpfTestHost.Run(() =>
        {
            var view = new ShopView(new AppState(), new ShopService(), _ => true,
                tier => tier != RarityTier.Legendary, () => { });
            WpfTestHost.Layout(view);

            var legendaryCard = (Border)view.FindName("EggLegendaryCard");
            var commonCard = (Border)view.FindName("EggCommonCard");

            Assert.False(((System.Windows.Controls.Button)view.FindName("EggLegendaryButton")).IsEnabled);
            Assert.True(legendaryCard.Opacity < 1.0);
            Assert.Equal(1.0, commonCard.Opacity);
        });

    [Fact]
    public void CatchAndSettingsViews_LayOut() =>
        WpfTestHost.Run(() =>
        {
            var appState = new AppState();
            var catchView = new CatchView(appState, () => null, _ => Task.FromResult<byte[]?>(null), () => false, () => { });
            WpfTestHost.Layout(catchView);

            var settingsView = new SettingsView(new SettingsViewModel(appState.Settings), () => { });
            WpfTestHost.Layout(settingsView);
        });

    [Fact]
    public void ShellAndOnboardingWindows_Construct() =>
        WpfTestHost.Run(() =>
        {
            var appState = new AppState();
            var menu = new AppMenuWindow(
                BuildCollectionView(),
                new SettingsView(new SettingsViewModel(appState.Settings), () => { }),
                new ShopView(appState, new ShopService(), _ => true, _ => true, () => { }),
                new CatchView(appState, () => null, _ => Task.FromResult<byte[]?>(null), () => false, () => { }));
            menu.SelectTab(2);

            new OnboardingWindow();

            var chains = new[] { new SpeciesEvolutionChain(10001, 10, 255, 3, new[] { 10, 11, 12 }) };
            new StarterSelectionWindow(chains, _ => Task.FromResult<byte[]?>(null));
        });

    private static CollectionView BuildCollectionView()
    {
        var appState = new AppState();
        var individual = new PokemonIndividual
        {
            SpeciesId = 4,
            EvolutionChainId = 4,
            ChainLength = 3,
            Nature = Nature.Hardy,
            IsShiny = true,
            CaughtAtUtc = DateTimeOffset.UtcNow
        };
        appState.Collection.Add(individual);
        appState.PendingEggs.Add(new PendingEgg { Tier = RarityTier.Rare, ReadyAtUtc = DateTimeOffset.UtcNow.AddMinutes(30) });

        var chains = new[] { new SpeciesEvolutionChain(4, 4, 45, 3, new[] { 4, 5, 6 }) };
        var viewModel = new CollectionViewModel(appState.Collection, appState.CareStateByIndividualId,
            new CareModeService(), CareMode.Chill, chains, appState.PendingEggs);

        var spriteCacheDirectory = Path.Combine(Path.GetTempPath(), "pokecorner-theme-tests", Guid.NewGuid().ToString("N"));
        return new CollectionView(viewModel, appState, new SpriteCache(spriteCacheDirectory),
            () => { }, () => { }, () => { }, () => null, _ => true);
    }
}
