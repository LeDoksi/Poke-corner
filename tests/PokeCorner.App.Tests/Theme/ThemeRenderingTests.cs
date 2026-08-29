using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
// Aliased rather than `using System.Windows.Shapes`: that namespace also has a Path, which
// collides with System.IO.Path (already used further down in this file for the sprite cache).
using Ellipse = System.Windows.Shapes.Ellipse;
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

    /// CollectionView's root is a Grid now, not the TabControl itself — the Pokédex detail card
    /// became an overlay layer inside the view (it used to be a separate Window), and an
    /// overlay needs a sibling to overlay. The tabs are still the first layer.
    private static TabControl CollectionTabs(CollectionView view) =>
        (TabControl)((Grid)view.Content).Children[0];

    [Fact]
    public void CollectionView_BothTabs_LayOutWithRealData() =>
        WpfTestHost.Run(() =>
        {
            var view = BuildCollectionView();
            WpfTestHost.Layout(view);

            var tabs = CollectionTabs(view);
            Assert.Equal(2, tabs.Items.Count);

            // Only the selected tab's content is realized, so the Pokédex card template is
            // exercised by switching tabs and laying out again.
            tabs.SelectedIndex = 1;
            WpfTestHost.Layout(view);
        });

    /// The roster's per-individual detail (dex number, nature, catch date) survived the merge
    /// into the single Pokédex grid — it moved onto the card model that feeds the detail
    /// popup, which is where the merge would most plausibly have dropped it on the floor.
    [Fact]
    public void CollectionView_PokedexCards_CarryDexNumberNatureAndCatchDate() =>
        WpfTestHost.Run(() =>
        {
            var view = BuildCollectionView();
            WpfTestHost.Layout(view);

            var grid = (ItemsControl)view.FindName("PokedexGrid");
            var card = Assert.IsType<PokedexCard>(Assert.Single(grid.ItemsSource.Cast<object>()));

            Assert.Equal("#004", card.DexNumber);
            Assert.Equal("Hardy", card.NatureText);
            Assert.Equal(DateTimeOffset.UtcNow.ToLocalTime().ToString("dd.MM.yyyy"), card.CaughtAtText);
            Assert.NotNull(card.RarityBrush);
            Assert.NotEqual(Guid.Empty, card.IndividualId);
        });

    /// Item 4: the "Мои покемоны" roster is gone entirely — one browsable list, not two
    /// parallel ones. Asserting on the named elements rather than a screenshot means
    /// re-adding the second list fails loudly instead of quietly returning the duplication.
    [Fact]
    public void CollectionView_HasNoSeparateRosterList_OnlyThePokedexGrid() =>
        WpfTestHost.Run(() =>
        {
            var view = BuildCollectionView();
            WpfTestHost.Layout(view);

            Assert.Null(view.FindName("EntriesList"));
            Assert.Null(view.FindName("RosterCountText"));
            Assert.NotNull(view.FindName("PokedexGrid"));
        });

    /// Item 4, the behaviour change underneath it: the grid is per-INDIVIDUAL now, so two
    /// owned copies of one species are two cards — while the headline completion stat still
    /// counts distinct species.
    [Fact]
    public void CollectionView_PokedexGrid_ShowsOneCardPerIndividualNotPerSpecies() =>
        WpfTestHost.Run(() =>
        {
            var appState = new AppState();
            appState.Collection.Add(NewIndividual(speciesId: 25, chainId: 25));
            appState.Collection.Add(NewIndividual(speciesId: 25, chainId: 25));
            var chains = new[]
            {
                new SpeciesEvolutionChain(25, 25, 190, 2, new[] { 25, 26 }, new[] { "Pikachu", "Raichu" })
            };
            var view = BuildCollectionView(appState, chains);
            WpfTestHost.Layout(view);

            var grid = (ItemsControl)view.FindName("PokedexGrid")!;
            var cards = grid.ItemsSource.Cast<PokedexCard>().ToList();

            Assert.Equal(2, cards.Count);
            Assert.All(cards, card => Assert.Equal("Pikachu", card.SpeciesName));
            Assert.Equal(2, cards.Select(card => card.IndividualId).Distinct().Count());

            // One species opened, two Pokémon owned — both reported.
            var countText = ((TextBlock)view.FindName("PokedexCountText")!).Text;
            Assert.Contains("1 вид", countText);
            Assert.Contains("2", countText);
        });

    // ---------------------------------------------------------------- user-feedback wave

    /// Item 1: the home tab is "what's active right now" only. Eggs and the owned-Pokémon
    /// grid live on the Pokédex tab, and this asserts on the actual visual ancestry rather
    /// than trusting that the XAML block was pasted under the right TabItem.
    /// (The roster list this used to also check is gone — see
    /// CollectionView_HasNoSeparateRosterList_OnlyThePokedexGrid.)
    [Fact]
    public void CollectionView_EggsAndTheGridLiveOnThePokedexTab_NotTheHomeTab() =>
        WpfTestHost.Run(() =>
        {
            var view = BuildCollectionView();
            WpfTestHost.Layout(view);

            var tabs = CollectionTabs(view);
            var homeTab = (TabItem)tabs.Items[0];
            var pokedexTab = (TabItem)tabs.Items[1];

            foreach (var name in new[] { "PendingEggsList", "EggsEmptyText", "PokedexGrid", "PokedexCountText" })
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

    /// Item 9: the three single-purpose dialogs no longer render inside the stock light OS
    /// title bar. All three must resolve the SHARED DialogWindowStyle — asserting on style
    /// identity rather than on each window's own markup is what keeps them from drifting apart
    /// again — and that style must suppress the OS chrome and supply the app's own header.
    ///
    /// StarterSelectionWindow is the reason this is a test rather than three screenshots: it
    /// only ever appears on a genuine first run, which is not reachable while a save file
    /// exists.
    [Fact]
    public void DialogWindows_AllUseTheSharedThemedChromeInsteadOfTheOsTitleBar() =>
        WpfTestHost.Run(() =>
        {
            var expected = (Style)Application.Current.Resources["DialogWindowStyle"];

            var windows = new System.Windows.Window[]
            {
                new PokeCorner.App.Onboarding.OnboardingWindow(),
                new PokeCorner.App.Onboarding.StarterSelectionWindow(
                    new List<SpeciesEvolutionChain>(),
                    _ => Task.FromResult<byte[]?>(null),
                    _ => Task.FromResult<IReadOnlyList<byte[]>?>(null)),
                new PokeCorner.App.Popup.ConfirmDialog("Заголовок", "Тело", "OK")
            };

            foreach (var window in windows)
            {
                Assert.Same(expected, window.Style);

                // No OS caption: the header comes from the style's own template instead.
                Assert.Equal(WindowStyle.None, window.WindowStyle);

                var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(window);
                Assert.NotNull(chrome);
                // The strip's height, and what makes it draggable without a DragMove handler.
                Assert.Equal(36, chrome.CaptionHeight);

                window.Close();
            }
        });

    /// Item 8: the Pokédex detail card is an in-view overlay, not a separate OS window, and a
    /// click on the dimmed backdrop closes it while a click on the card itself does not.
    /// Drives the real routed events the app uses rather than calling the handlers directly,
    /// so the card's own e.Handled=true is genuinely what stops the backdrop from firing.
    [Fact]
    public void CollectionView_PokedexDetail_IsAnInViewOverlayThatClosesOnOutsideClick() =>
        WpfTestHost.Run(() =>
        {
            var view = BuildCollectionView();
            WpfTestHost.Layout(view);
            CollectionTabs(view).SelectedIndex = 1; // the grid only realizes on its own tab
            WpfTestHost.Layout(view);

            var overlay = (Border)view.FindName("DetailOverlay")!;
            Assert.Equal(Visibility.Collapsed, overlay.Visibility);
            Assert.True(Panel.GetZIndex(overlay) > 0, "the detail overlay must sit above the tabs");

            // No second Window anywhere: the whole point of the change.
            Assert.DoesNotContain(
                System.Windows.Application.Current.Windows.Cast<System.Windows.Window>(),
                w => w.GetType().Name == "PokemonDetailWindow");

            var card = Descendants(view).OfType<Border>()
                .First(b => b.DataContext is PokedexCard && b.Tag is Guid);
            card.RaiseEvent(MouseDown(UIElement.MouseLeftButtonUpEvent));
            Assert.Equal(Visibility.Visible, overlay.Visibility);
            // The overlay renders the card that was clicked, not a blank shell.
            Assert.Equal(((PokedexCard)card.DataContext).SpeciesName,
                ((TextBlock)view.FindName("DetailSpeciesNameText")!).Text);

            // A click INSIDE the card must not close it.
            var detailCard = (Border)overlay.Child;
            detailCard.RaiseEvent(MouseDown(UIElement.MouseLeftButtonDownEvent));
            Assert.Equal(Visibility.Visible, overlay.Visibility);

            // A click on the dimmed backdrop must.
            overlay.RaiseEvent(MouseDown(UIElement.MouseLeftButtonDownEvent));
            Assert.Equal(Visibility.Collapsed, overlay.Visibility);
        });

    private static System.Windows.Input.MouseButtonEventArgs MouseDown(RoutedEvent routedEvent) =>
        new(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
        {
            RoutedEvent = routedEvent
        };

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
                new SpeciesEvolutionChain(4, 4, 45, 3, new[] { 4, 5, 6 }, new[] { "Charmander", "Charmeleon", "Charizard" }),
                new SpeciesEvolutionChain(16, 16, 255, 3, new[] { 16, 17, 18 }, new[] { "Pidgey", "Pidgeotto", "Pidgeot" })
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
                new SpeciesEvolutionChain(4, 4, 45, 3, new[] { 4, 5, 6 }, new[] { "Charmander", "Charmeleon", "Charizard" }),
                new SpeciesEvolutionChain(16, 16, 255, 3, new[] { 16, 17, 18 }, new[] { "Pidgey", "Pidgeotto", "Pidgeot" })
            };
            var view = BuildCollectionView(appState, chains);
            WpfTestHost.Layout(view);

            ((RadioButton)view.FindName("RarityFilterRare")!).IsChecked = true;
            view.Refresh();

            var grid = (ItemsControl)view.FindName("PokedexGrid")!;
            Assert.Equal(4, Assert.Single(grid.ItemsSource.Cast<PokedexCard>()).SpeciesId);
        });

    /// Item 2b: the pinned individual is first in the grid and its star renders filled gold.
    /// Still asserted on the card model after the roster/grid merge — the star moved into the
    /// detail popup, but the pin still drives sort order and the card's gold marker.
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
                new SpeciesEvolutionChain(4, 4, 45, 3, new[] { 4, 5, 6 }, new[] { "Charmander", "Charmeleon", "Charizard" }),
                new SpeciesEvolutionChain(16, 16, 255, 3, new[] { 16, 17, 18 }, new[] { "Pidgey", "Pidgeotto", "Pidgeot" })
            });
            WpfTestHost.Layout(view);

            var grid = (ItemsControl)view.FindName("PokedexGrid")!;
            var cards = grid.ItemsSource.Cast<PokedexCard>().ToList();

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

    /// Lays the panel out and returns its content root, so the assertions below can read real
    /// ActualWidth/ActualHeight rather than the unarranged defaults.
    private static FrameworkElement LayOutProgressPanel(PokeCorner.App.Overlay.ProgressPanelWindow panel)
    {
        var root = (FrameworkElement)panel.Content;
        root.Measure(new Size(panel.Width, double.PositiveInfinity));
        root.Arrange(new Rect(0, 0, panel.Width, root.DesiredSize.Height));
        root.UpdateLayout();
        return root;
    }

    /// The full circumference of one gauge ring in StrokeDashArray's units, derived from the
    /// arc's OWN laid-out size rather than from a number copied out of the code-behind. That is
    /// the point: if ProgressPanelWindow's GaugeDiameter/GaugeStrokeThickness constants ever
    /// drift from the XAML that actually sizes the Ellipse, every fraction below goes wrong and
    /// these tests say so.
    private static double DashUnitsPerTurn(System.Windows.Shapes.Ellipse arc) =>
        Math.PI * (arc.ActualWidth - arc.StrokeThickness) / arc.StrokeThickness;

    /// Item 1: the desktop panel is three filling CIRCLES again (the intervening redesign's
    /// vertical bars were not what was asked for), now stacked in a vertical column. Each ring
    /// is an Ellipse whose StrokeDashArray makes one dash cover `fraction` of the circumference
    /// — so "the gauge is filled correctly" is exactly "dash length == circumference * fraction".
    [Fact]
    public void ProgressPanelWindow_CircularGaugesFillTheirFractionOfTheRing() =>
        WpfTestHost.Run(() =>
        {
            var panel = new PokeCorner.App.Overlay.ProgressPanelWindow();
            panel.UpdateProgress(new ProgressInfo(0.5, 0.25, 0.75, EvolutionProgressState.InProgress));
            LayOutProgressPanel(panel);

            var expected = new Dictionary<string, double>
            {
                ["Catch"] = 0.5,
                ["Evolution"] = 0.25,
                ["Treat"] = 0.75
            };

            foreach (var (concept, fraction) in expected)
            {
                var arc = (System.Windows.Shapes.Ellipse)panel.FindName($"{concept}Arc")!;

                Assert.Equal(DashUnitsPerTurn(arc) * fraction, arc.StrokeDashArray[0], precision: 2);

                // Rotated so the ring starts filling at 12 o'clock, not 3 o'clock.
                Assert.Equal(-90, ((RotateTransform)arc.RenderTransform).Angle);
            }
        });

    /// An empty gauge must render as nothing at all. It cannot simply be a zero-length dash:
    /// StrokeDashCap="Round" paints a zero-length dash as a round dot, which would leave a
    /// stray pip at 12 o'clock on an empty ring — so the arc is hidden outright instead.
    /// A full one must close the ring rather than stopping short.
    [Fact]
    public void ProgressPanelWindow_EmptyGaugeIsHiddenAndAFullGaugeClosesTheRing() =>
        WpfTestHost.Run(() =>
        {
            var panel = new PokeCorner.App.Overlay.ProgressPanelWindow();
            panel.UpdateProgress(new ProgressInfo(0.0, 1.0, 0.0, EvolutionProgressState.InProgress));
            LayOutProgressPanel(panel);

            Assert.Equal(Visibility.Hidden, ((System.Windows.Shapes.Ellipse)panel.FindName("CatchArc")!).Visibility);
            Assert.Equal(Visibility.Hidden, ((System.Windows.Shapes.Ellipse)panel.FindName("TreatArc")!).Visibility);

            var full = (System.Windows.Shapes.Ellipse)panel.FindName("EvolutionArc")!;
            Assert.Equal(Visibility.Visible, full.Visibility);
            Assert.Equal(DashUnitsPerTurn(full), full.StrokeDashArray[0], precision: 2);
        });

    /// The panel keeps a real explicit width (the sizing-collapse fix), and item 1's actual
    /// ask: the three gauges stand one ABOVE another in a single narrow column to the pet's
    /// left, each with its own semantic colour AND its own icon inside the ring — the icon
    /// being the half a colour alone can't say.
    [Fact]
    public void ProgressPanelWindow_IsANarrowVerticalColumnOfIconLabelledGauges() =>
        WpfTestHost.Run(() =>
        {
            var panel = new PokeCorner.App.Overlay.ProgressPanelWindow();
            panel.UpdateProgress(new ProgressInfo(0.5, 0.25, 0.75, EvolutionProgressState.InProgress));
            var root = LayOutProgressPanel(panel);

            Assert.True(panel.Width > 0, "the panel collapsed to zero width");
            Assert.Equal(SizeToContent.Height, panel.SizeToContent);

            // Narrow: one gauge wide, so it fits beside the pet without fighting the taskbar.
            Assert.True(panel.Width <= 80, $"panel is {panel.Width}px wide");

            var arcs = new[] { "CatchArc", "EvolutionArc", "TreatArc" }
                .Select(name => (System.Windows.Shapes.Ellipse)panel.FindName(name)!)
                .ToList();

            var colors = arcs.Select(a => ((SolidColorBrush)a.Stroke).Color).ToList();
            Assert.Equal(3, colors.Distinct().Count());

            // Stacked one under the other, NOT side by side: each gauge's top is strictly
            // below the previous one's, and they all share the same left edge.
            var tops = arcs.Select(a => a.TransformToAncestor(root).Transform(new Point(0, 0))).ToList();
            Assert.True(tops[1].Y > tops[0].Y, "evolution gauge is not below the catch gauge");
            Assert.True(tops[2].Y > tops[1].Y, "treat gauge is not below the evolution gauge");
            Assert.Equal(tops[0].X, tops[1].X, precision: 2);
            Assert.Equal(tops[0].X, tops[2].X, precision: 2);

            // Big enough to read a fill level on at a glance.
            foreach (var arc in arcs)
            {
                Assert.True(arc.ActualWidth >= 30, $"gauge is only {arc.ActualWidth}px across");
            }

            // One icon per concept, and they must be the SHARED theme styles so the in-app
            // progress card renders the identical glyphs.
            var icons = Descendants(root).OfType<ContentControl>().ToList();
            var iconStyles = icons.Select(i => i.Style).ToList();
            Assert.Equal(3, iconStyles.Distinct().Count());
            foreach (var key in new[] { "CatchIconStyle", "EvolutionIconStyle", "TreatIconStyle" })
            {
                Assert.Contains(Application.Current.Resources[key], iconStyles);
            }
        });

    /// Item 6, the other half of "the same icon in both places": the in-app "Прогресс" card
    /// must render the identical shared styles the desktop panel does. Asserting on style
    /// identity — not on shape — is what actually pins the two surfaces together, since a
    /// duplicated inline Path would look the same today and drift tomorrow.
    [Fact]
    public void CollectionView_ProgressCard_UsesTheSameSharedConceptIconsAsTheDesktopPanel() =>
        WpfTestHost.Run(() =>
        {
            var view = BuildCollectionView();
            WpfTestHost.Layout(view);

            var iconStyles = Descendants(view).OfType<ContentControl>()
                .Select(control => control.Style)
                .ToList();

            foreach (var key in new[] { "CatchIconStyle", "EvolutionIconStyle", "TreatIconStyle" })
            {
                Assert.Contains(Application.Current.Resources[key], iconStyles);
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

    /// "Супер Хардкор" only does anything in Hardcore (App.CheckHardcoreNeglect returns early
    /// for Chill), so the checkbox must be greyed out otherwise — on the initial load as well
    /// as on every later change of the combo, which is the half that is easy to forget.
    [Theory]
    [InlineData(CareMode.Chill, false)]
    [InlineData(CareMode.Hardcore, true)]
    public void SettingsView_SuperHardcoreCheckbox_IsEnabledOnlyInHardcore(CareMode mode, bool expectedEnabled) =>
        WpfTestHost.Run(() =>
        {
            var settings = new AppState().Settings;
            settings.CareMode = mode;

            var view = new SettingsView(new SettingsViewModel(settings), () => { });
            WpfTestHost.Layout(view);

            var check = (CheckBox)view.FindName("SuperHardcoreCheck")!;
            Assert.Equal(expectedEnabled, check.IsEnabled);
        });

    /// Item 5: switching the combo re-gates the checkbox AND clears it.
    ///
    /// This reverses what a prior wave asserted here. That wave deliberately kept the stored
    /// Super Hardcore choice when leaving Hardcore, on the theory that it preserved the
    /// player's earlier decision — the checkbox went disabled but stayed visibly ticked. Live
    /// testing rejected it: a ticked checkbox describing a permadeath rule that is not in
    /// force reads as broken, not as thoughtful. Leaving Hardcore now unchecks it and clears
    /// the setting, so what the UI shows and what is stored can't disagree.
    [Fact]
    public void SettingsView_LeavingHardcore_UnchecksAndClearsSuperHardcore() =>
        WpfTestHost.Run(() =>
        {
            var settings = new AppState().Settings;
            settings.CareMode = CareMode.Hardcore;
            settings.SuperHardcoreEnabled = true;

            var view = new SettingsView(new SettingsViewModel(settings), () => { });
            WpfTestHost.Layout(view);

            var combo = (ComboBox)view.FindName("CareModeCombo")!;
            var check = (CheckBox)view.FindName("SuperHardcoreCheck")!;
            Assert.True(check.IsEnabled);
            Assert.True(check.IsChecked);

            combo.SelectedIndex = 0; // Chill
            Assert.False(check.IsEnabled);
            Assert.False(check.IsChecked, "the box must not stay ticked once the mode can't use it");
            Assert.False(settings.SuperHardcoreEnabled, "the stored setting must not linger past the UI");

            // Coming back to Hardcore re-enables it, unticked — the player opts in again
            // deliberately rather than silently inheriting a permadeath rule.
            combo.SelectedIndex = 1;
            Assert.True(check.IsEnabled);
            Assert.False(check.IsChecked);
            Assert.False(settings.SuperHardcoreEnabled);
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
                new SpeciesEvolutionChain(10001, 10, 255, 3, new[] { 10, 11, 12 }, new[] { "Caterpie", "Metapod", "Butterfree" }),
                new SpeciesEvolutionChain(10002, 20, 255, 3, new[] { 20, 21, 22 }, new[] { "Raticate", "Spearow", "Fearow" }),
                new SpeciesEvolutionChain(10003, 30, 255, 3, new[] { 30, 31, 32 }, new[] { "Nidorina", "Nidoqueen", "Nidoran M" })
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
                    new CatchView(appState, () => null, _ => Task.FromResult<byte[]?>(null), () => false, () => { }, id => $"Pokemon #{id}")),
                // The reset flow's confirm dialog exists specifically so the destructive
                // prompt isn't a stock white MessageBox — so it belongs in this check.
                new ConfirmDialog("Заголовок", "Тело", "Удалить")
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
                new SpeciesEvolutionChain(10001, 10, 255, 3, new[] { 10, 11, 12 }, new[] { "Caterpie", "Metapod", "Butterfree" }),
                new SpeciesEvolutionChain(10002, 20, 255, 3, new[] { 20, 21, 22 }, new[] { "Raticate", "Spearow", "Fearow" }),
                new SpeciesEvolutionChain(10003, 30, 255, 3, new[] { 30, 31, 32 }, new[] { "Nidorina", "Nidoqueen", "Nidoran M" })
            };
            var window = new StarterSelectionWindow(chains, _ => Task.FromResult<byte[]?>(null));
            var root = (FrameworkElement)window.Content;
            // Explicit height, not window.Height: the picker is SizeToContent="Height" now
            // that the cards carry a rarity tag, so window.Height is NaN.
            WpfTestHost.Layout(root, window.Width, 600);

            Assert.Empty(Descendants(root).OfType<TabControl>());
            Assert.Empty(Descendants(root).OfType<ComboBox>());

            // Item 2: every candidate card shows its rarity, sourced from the chain's own
            // capture rate through the same classifier the Pokédex cards use. Capture rate
            // 255 is Common for all three chains above.
            var expected = RarityDisplay.GetName(RarityTier.Common);
            foreach (var name in new[] { "Candidate1RarityText", "Candidate2RarityText", "Candidate3RarityText" })
            {
                Assert.Equal(expected, ((TextBlock)window.FindName(name)!).Text);
            }

            // And the tag is tinted with the tier's brush, not left on the default foreground.
            var commonBrush = RarityDisplay.GetBrush(RarityTier.Common);
            Assert.Same(commonBrush, ((Ellipse)window.FindName("Candidate1RarityDot")!).Fill);
            Assert.Same(commonBrush, ((Border)window.FindName("Candidate1RarityPill")!).BorderBrush);
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
            () => { }, () => { }, () => { }, getActiveSpriteFrames ?? (() => null), _ => true,
            // Ready-to-evolve is true so the "Эволюционировать!" button is actually visible
            // during these layout/theme assertions rather than collapsed out of the tree.
            () => true, () => true);
    }

    /// The badge must carry the type's NAME as rendered text, not just a colour. Asserted by
    /// walking the realized visual tree for a TextBlock actually reading "Fire" — a colour-only
    /// swatch, or a badge whose text got dropped for space, fails here.
    [Fact]
    public void CollectionView_TypeBadges_RenderTheTypeNameAsVisibleText() =>
        WpfTestHost.Run(() =>
        {
            var appState = new AppState();
            appState.Collection.Add(NewIndividual(speciesId: 4, chainId: 4));
            var chains = new[]
            {
                new SpeciesEvolutionChain(4, 4, 45, 3, new[] { 4, 5, 6 },
                    new[] { "Charmander", "Charmeleon", "Charizard" },
                    new[] { new[] { "fire" }, new[] { "fire" }, new[] { "fire", "flying" } })
            };
            var view = BuildCollectionView(appState, chains);
            WpfTestHost.Layout(view);

            var badges = (System.Windows.Controls.ItemsControl)view.FindName("ActiveTypeBadges")!;
            var labels = Descendants<TextBlock>(badges).Select(t => t.Text).ToList();

            Assert.Contains("Fire", labels);
            Assert.All(Descendants<TextBlock>(badges), t => Assert.False(string.IsNullOrWhiteSpace(t.Text)));
        });

    /// Part A, at the pixel level: a single-stage individual must NOT show "100%" anywhere on
    /// the progress card — it must say it does not evolve, with an empty bar.
    [Fact]
    public void CollectionView_SingleStageIndividual_ShowsNeverEvolvesInsteadOf100Percent() =>
        WpfTestHost.Run(() =>
        {
            var appState = new AppState();
            appState.Collection.Add(new PokemonIndividual
            {
                SpeciesId = 587, EvolutionChainId = 298, ChainLength = 1,
                Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow
            });
            var chains = new[]
            {
                new SpeciesEvolutionChain(298, 587, 200, 1, new[] { 587 }, new[] { "Emolga" },
                    new[] { new[] { "electric", "flying" } })
            };
            var view = BuildCollectionView(appState, chains);
            WpfTestHost.Layout(view);

            var label = (TextBlock)view.FindName("EvolutionPercentText")!;
            var bar = (System.Windows.Controls.ProgressBar)view.FindName("EvolutionProgressBar")!;

            Assert.Equal(EvolutionProgressDisplay.NeverEvolvesLabel, label.Text);
            Assert.DoesNotContain("%", label.Text);
            Assert.Equal(0.0, bar.Value);
            Assert.True(bar.Opacity < 1.0, "a bar that can never fill should be dimmed");
        });

    /// The other terminal state: a fully-evolved individual keeps its FULL bar (it earned it)
    /// but is labelled as a final form rather than as a countdown sitting at 100%.
    [Fact]
    public void CollectionView_FullyEvolvedIndividual_ShowsFinalFormWithAFullBar() =>
        WpfTestHost.Run(() =>
        {
            var appState = new AppState();
            var individual = NewIndividual(speciesId: 4, chainId: 4);
            individual.CurrentStage = EvolutionStage.Stage3;
            appState.Collection.Add(individual);
            var chains = new[]
            {
                new SpeciesEvolutionChain(4, 4, 45, 3, new[] { 4, 5, 6 },
                    new[] { "Charmander", "Charmeleon", "Charizard" },
                    new[] { new[] { "fire" }, new[] { "fire" }, new[] { "fire", "flying" } })
            };
            var view = BuildCollectionView(appState, chains);
            WpfTestHost.Layout(view);

            var label = (TextBlock)view.FindName("EvolutionPercentText")!;
            var bar = (System.Windows.Controls.ProgressBar)view.FindName("EvolutionProgressBar")!;

            Assert.Equal(EvolutionProgressDisplay.FinalFormLabel, label.Text);
            Assert.Equal(1.0, bar.Value);
            Assert.Equal(1.0, bar.Opacity);
        });

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
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
            var catchView = new CatchView(appState, () => null, _ => Task.FromResult<byte[]?>(null), () => false, () => { }, id => $"Pokemon #{id}");
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
                new CatchView(appState, () => null, _ => Task.FromResult<byte[]?>(null), () => false, () => { }, id => $"Pokemon #{id}"));
            menu.SelectTab(2);

            new OnboardingWindow();

            var chains = new[] { new SpeciesEvolutionChain(10001, 10, 255, 3, new[] { 10, 11, 12 }, new[] { "Caterpie", "Metapod", "Butterfree" }) };
            new StarterSelectionWindow(chains, _ => Task.FromResult<byte[]?>(null));
        });

    /// The reported bug, measured: on a REAL 3-stage line the strip's outer two stage cards
    /// were cut off at both edges of the detail card. Three 92px cards plus two arrows needed
    /// 316 DIPs inside a well that offers 300, so the row simply did not fit.
    ///
    /// Measured by SUMMING the row's children, which is the only figure here that is not
    /// already clamped to what fits. Neither of the two obvious measurements works: WPF clamps
    /// every element's DesiredSize to the constraint it was measured with, and arrange clamps
    /// ActualWidth the same way — so both the ItemsControl's and the row panel's own numbers
    /// report "fits" at exactly the moment the content is being clipped. (Checked: against the
    /// pre-fix 92px cards, a row-panel DesiredSize assertion passes and proves nothing.) A
    /// horizontal StackPanel measures each child with infinite width, so the CHILDREN's
    /// desired widths are unclamped and their sum is the width the row genuinely wants.
    ///
    /// Three stages is the widest case that can ever occur — PokeApiClient.CollectStages keeps
    /// only the longest branch of the chain tree — so this is the bound, not a sample.
    [Fact]
    public void CollectionView_EvolutionChainStrip_ThreeStages_FitsInsideTheDetailCard() =>
        WpfTestHost.Run(() =>
        {
            // The user's own case: a Luxio, mid-line, so the third stage renders as the masked
            // "?" mystery card and all three stage cards are present at once.
            var appState = new AppState();
            appState.Collection.Add(new PokemonIndividual
            {
                SpeciesId = 403,
                EvolutionChainId = 213,
                ChainLength = 3,
                CurrentStage = EvolutionStage.Stage2,
                Nature = Nature.Hardy,
                IsShiny = false,
                CaughtAtUtc = DateTimeOffset.UtcNow
            });
            var chains = new[]
            {
                new SpeciesEvolutionChain(213, 403, 235, 3, new[] { 403, 404, 405 },
                    new[] { "Shinx", "Luxio", "Luxray" },
                    new[] { new[] { "electric" }, new[] { "electric" }, new[] { "electric" } })
            };

            var view = BuildCollectionView(appState, chains);
            WpfTestHost.Layout(view);
            CollectionTabs(view).SelectedIndex = 1; // the grid only realizes on its own tab
            WpfTestHost.Layout(view);

            var card = Descendants(view).OfType<Border>()
                .First(b => b.DataContext is PokedexCard && b.Tag is Guid);
            card.RaiseEvent(MouseDown(UIElement.MouseLeftButtonUpEvent));
            WpfTestHost.Layout(view);

            var section = (Border)view.FindName("DetailEvolutionChainSection")!;
            var strip = (ItemsControl)view.FindName("DetailEvolutionChainStrip")!;
            Assert.Equal(Visibility.Visible, section.Visibility);
            Assert.Equal(3, strip.Items.Count);

            var row = Descendants<StackPanel>(strip).First(); // the ItemsPanel
            var needed = row.Children.Cast<UIElement>().Sum(child => child.DesiredSize.Width);
            var available = section.ActualWidth
                - section.Padding.Left - section.Padding.Right
                - section.BorderThickness.Left - section.BorderThickness.Right;

            Assert.True(needed <= available,
                $"the 3-stage chain row wants {needed} DIPs but the well only offers {available} — the outer cards are clipped");
        });

    /// The reported tooltip bug, measured. The theme caps every tooltip at MaxWidth 280 and
    /// its template used to render the string through a bare ContentPresenter, which produces
    /// a TextBlock that does NOT wrap — so the ЛАКОМСТВА tooltip was hard-clipped at roughly
    /// 40 characters, on screen as "…активностью и тр".
    ///
    /// The proof is the laid-out height: one line means the tail of the string was cut off,
    /// two or more means the whole string is actually being rendered.
    [Fact]
    public void ToolTip_TextLongerThanItsMaxWidth_WrapsInsteadOfBeingClipped() =>
        WpfTestHost.Run(() =>
        {
            // Verbatim from CollectionView.xaml's ЛАКОМСТВА stat well — the reported instance.
            const string treatsToolTip = "Лакомства зарабатываются активностью и тратятся на кормление";

            var tip = new ToolTip { Content = treatsToolTip };
            tip.IsOpen = true; // a ToolTip only builds its template once hosted in its popup
            try
            {
                tip.UpdateLayout();

                var text = Descendants<TextBlock>(tip).Single();
                Assert.Equal(treatsToolTip, text.Text);
                Assert.Equal(TextWrapping.Wrap, text.TextWrapping);

                // Fits the cap horizontally...
                Assert.True(text.ActualWidth <= tip.MaxWidth,
                    $"the tooltip text measured {text.ActualWidth} DIPs against a {tip.MaxWidth} cap");

                // ...and, because it fits by WRAPPING rather than by being cut, occupies more
                // than the single line the clipped version was squeezed onto.
                var singleLineHeight = text.FontSize * text.FontFamily.LineSpacing;
                Assert.True(text.ActualHeight > singleLineHeight * 1.5,
                    $"the tooltip rendered {text.ActualHeight} DIPs tall — one line of {singleLineHeight}, i.e. still clipped rather than wrapped");
            }
            finally
            {
                tip.IsOpen = false;
            }
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

        var chains = new[] { new SpeciesEvolutionChain(4, 4, 45, 3, new[] { 4, 5, 6 }, new[] { "Charmander", "Charmeleon", "Charizard" }) };
        var viewModel = new CollectionViewModel(appState.Collection, appState.CareStateByIndividualId,
            new CareModeService(), CareMode.Chill, chains, appState.PendingEggs);

        var spriteCacheDirectory = Path.Combine(Path.GetTempPath(), "pokecorner-theme-tests", Guid.NewGuid().ToString("N"));
        return new CollectionView(viewModel, appState, new SpriteCache(spriteCacheDirectory),
            () => { }, () => { }, () => { }, () => null, _ => true, () => true, () => true);
    }
}
