using System.Windows;
using System.Windows.Controls;
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

            var settingsView = new SettingsView(new SettingsViewModel(appState.Settings));
            WpfTestHost.Layout(settingsView);
        });

    [Fact]
    public void ShellAndOnboardingWindows_Construct() =>
        WpfTestHost.Run(() =>
        {
            var appState = new AppState();
            var menu = new AppMenuWindow(
                BuildCollectionView(),
                new SettingsView(new SettingsViewModel(appState.Settings)),
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
