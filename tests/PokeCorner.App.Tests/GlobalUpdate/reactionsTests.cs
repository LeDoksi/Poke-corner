using System.Windows;
using System.Windows.Controls;
using PokeCorner.App.Overlay;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Tests.GlobalUpdate;

public sealed class ReactionsTests
{
    [Fact]
    public void ConfirmedCatchRemainsVisibleWhenPlaybackIsInterrupted()
    {
        WpfTestHost.Run(() =>
        {
            var state = new AppState();
            var view = new CatchView(state, () => null, _ => Task.FromResult<byte[]?>(null),
                () => 0, () => { }, _ => "Bulbasaur");
            var result = new CatchReveal(new CatchResult(1, 1, Nature.Bold, false, false, 3), RarityTier.Common);

            _ = view.PlayReveal(result);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

            Assert.Equal("Продолжить", ((Button)view.FindName("CatchButton")!).Content);
            Assert.Equal(Visibility.Visible, ((TextBlock)view.FindName("RevealNameText")!).Visibility);
            Assert.Contains("Bulbasaur", ((TextBlock)view.FindName("RevealNameText")!).Text);
        });
    }

    [Fact]
    public async Task SlowSpriteFetchDoesNotBlockContinueAfterInterruptedCatch()
    {
        var sprite = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CatchView? view = null;
        Task? playback = null;
        WpfTestHost.Run(() =>
        {
            view = new CatchView(new AppState(), () => null, _ => sprite.Task,
                () => 0, () => { }, _ => "Bulbasaur");
            playback = view.PlayReveal(new CatchReveal(new CatchResult(1, 1, Nature.Bold, false, false, 3), RarityTier.Common));
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
        await playback!.WaitAsync(TimeSpan.FromSeconds(5));
        WpfTestHost.Run(() =>
        {
            var button = (Button)view!.FindName("CatchButton")!;
            Assert.True(button.IsEnabled);
            Assert.Equal("Продолжить", button.Content);
        });
        sprite.SetResult(null);
    }

    [Fact]
    public void RecreatedCatchViewCanShowTheSameConfirmedResultWithoutAnotherOpen()
    {
        WpfTestHost.Run(() =>
        {
            var state = new AppState();
            var opens = 0;
            var acknowledged = false;
            CatchView NewView() => new(state, () => { opens++; return null; },
                _ => Task.FromResult<byte[]?>(null), () => 0, () => { }, _ => "Bulbasaur",
                onResultContinued: () => acknowledged = true);
            var confirmed = new CatchReveal(new CatchResult(1, 1, Nature.Bold, false, false, 3), RarityTier.Common);
            var first = NewView();
            _ = first.PlayReveal(confirmed);
            first.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            var second = NewView();
            second.RestoreReveal(confirmed);

            Assert.Equal("Продолжить", ((Button)second.FindName("CatchButton")!).Content);
            Assert.Contains("Bulbasaur", ((TextBlock)second.FindName("RevealNameText")!).Text);
            Assert.Equal(0, opens);
            Assert.False(acknowledged);
            ((Button)second.FindName("CatchButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(acknowledged);
            Assert.Equal(0, opens);
        });
    }

    [Fact]
    public void RepeatedPettingKeepsOnlyOneBoundedVisualEffect()
    {
        WpfTestHost.Run(() =>
        {
            var overlay = new OverlayWindow();
            for (var click = 0; click < 20; click++) overlay.PlayPetReaction();
            var hearts = (Panel)overlay.FindName("HeartEffects")!;
            Assert.False(hearts.IsHitTestVisible);
            Assert.InRange(hearts.Children.Count, 1, 3);
        });
    }

    [Theory]
    [InlineData(2.99, 0)]
    [InlineData(3, 1)]
    [InlineData(4.5, 2)]
    [InlineData(5.75, 3)]
    [InlineData(24, 3)]
    public void YarnStageFollowsSavedLastInteraction(double hours, int expected)
    {
        WpfTestHost.Run(() =>
        {
            var overlay = new OverlayWindow();
            var now = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
            overlay.UpdateThought(now.AddHours(-hours), now);
            var cloud = (FrameworkElement)overlay.FindName("ThoughtBubble")!;
            Assert.Equal(expected == 0 ? Visibility.Collapsed : Visibility.Visible, cloud.Visibility);
            Assert.Equal(expected, cloud.Tag);
        });
    }

    [Fact]
    public void FeedingCrumbsStayBoundedAndClickThrough()
    {
        WpfTestHost.Run(() =>
        {
            var overlay = new OverlayWindow();
            for (var feed = 0; feed < 20; feed++) overlay.PlayFeedReaction();
            var crumbs = (Panel)overlay.FindName("CrumbEffects")!;
            Assert.False(crumbs.IsHitTestVisible);
            Assert.InRange(crumbs.Children.Count, 1, 3);
        });
    }

    [Fact]
    public void InterruptedHatchCanContinueFromItsSavedChild()
    {
        WpfTestHost.Run(() =>
        {
            var catalog = CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data"));
            var recipe = new NurseryCatalog(catalog).MixedRecipes.First(r => r.IsAvailable && r.SelectionUseful);
            PokemonIndividual Parent(int speciesId) => new()
            {
                SpeciesId = speciesId, CurrentSpeciesId = speciesId, FamilyRootId = catalog.Species.Single(s => s.Id == speciesId).FamilyRootId,
                EvolutionChainId = catalog.Species.Single(s => s.Id == speciesId).EvolutionChainId,
                ChainLength = 1, ReachedSpeciesIds = [speciesId], Nature = Nature.Bold, IsShiny = false,
                CaughtAtUtc = DateTimeOffset.UtcNow, FirstTraitId = "gatherer"
            };
            var data = new SaveData
            {
                SchemaVersion = 2, CatalogRevision = catalog.CatalogRevision, BalanceRevision = "test",
                MigrationId = SaveMigration.MigrationId, Settings = new(),
                Berries = SaveMigration.BerryIds.ToDictionary(id => id, _ => 100),
                Items = SaveMigration.ItemIds.ToDictionary(id => id, _ => 3),
                BerryRemainders = SaveMigration.BerryIds.ToDictionary(id => id, _ => new RationalRemainder()),
                Individuals = [Parent(recipe.FirstParentId), Parent(recipe.SecondParentId)]
            };
            var service = new NurseryService(catalog, data);
            var request = new NurseryRequest(data.Individuals.Select(p => p.Id).ToArray(), null,
                Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-9));
            service.Start(request);
            service.RefreshReady(DateTimeOffset.UtcNow);
            NurseryView View() => new(() => data, (ids, selected) => service.Preview(ids, selected),
                r => Task.FromResult(service.Start(r)), (id, op) => Task.FromResult(service.Hatch(id, op)),
                now => Task.FromResult(service.RefreshReady(now)), catalog, (_, _) => { }, _ => { }, _ => Task.CompletedTask);
            var view = View();
            ((Button)view.FindName("HatchButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var child = data.HatchedNurseryResults.Single().Value;
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

            Assert.Equal(child.Id, data.HatchedNurseryResults.Single().Value.Id);
            Assert.Single(data.Individuals.Where(p => p.Id == child.Id));
            var continueButton = (Button)view.FindName("HatchContinueButton")!;
            Assert.Equal(Visibility.Visible, continueButton.Visibility);
            continueButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(Visibility.Visible, ((FrameworkElement)view.FindName("ResultPanel")!).Visibility);
            Assert.Single(data.Individuals.Where(p => p.Id == child.Id));
            Assert.Equal(Visibility.Visible, ((FrameworkElement)View().FindName("ResultPanel")!).Visibility);
        });
    }

    [Fact]
    public void CrunchAssetIsInTheRuntimeDataPackage() =>
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "Data", "voice-preview", "audio", "feeding-crunch.wav")));

    [Fact]
    public void EffectWindowFitsInsideThePrimaryWorkAreaAtSmallPetSize()
    {
        WpfTestHost.Run(() =>
        {
            var overlay = new OverlayWindow();
            overlay.PositionAt(ScreenCorner.TopLeft, -1, 48);
            var dpi = OverlayWindow.DpiFor(overlay);
            var area = OverlayWindow.ToDips(System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea, dpi);
            Assert.True(overlay.Left >= area.Left && overlay.Top >= area.Top);
            Assert.True(overlay.Left + overlay.Width <= area.Right && overlay.Top + overlay.Height <= area.Bottom);
        });
    }
}
