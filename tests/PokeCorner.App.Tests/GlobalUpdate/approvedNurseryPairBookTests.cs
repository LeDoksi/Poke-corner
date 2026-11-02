using System.Windows;
using System.Windows.Controls;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Tests.GlobalUpdate;

// "Книга пар" and the in-place top-up (Georgy 01.11).
public sealed partial class NurseryUiTests
{
    private static Button[] PairRows(NurseryView view) =>
        ((StackPanel)view.FindName("PairListPanel")!).Children.OfType<Button>().ToArray();

    [Fact]
    public void PairBookListsTheCollectionsPairWithItsBabiesAndOneClickMakesItReady()
    {
        WpfTestHost.Run(() =>
        {
            var recipe = new NurseryCatalog(Catalog.Value).MixedRecipes.First(r => r.IsAvailable && r.SelectionUseful);
            var (data, first, second) = State(recipe);
            var view = View(data);
            WpfTestHost.Layout(view, 1064, 616);
            var row = Assert.Single(PairRows(view));
            var pair = Assert.IsType<NurseryPairOption>(row.Tag);
            Assert.True(pair.Matches(recipe.FirstParentId, recipe.SecondParentId));
            Assert.Equal(recipe.AvailableOutcomes.Count, pair.Outcomes.Count);
            Assert.InRange(pair.Outcomes.Sum(o => o.Percent), 98, 102);
            // Nothing from these babies is known yet, so the pair is listed under "Дадут новых".
            Assert.Contains("· 1", (string)((Button)view.FindName("NewPairsButton")!).Content);
            Assert.False(((Button)view.FindName("StartButton")!).IsEnabled);
            row.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(new[] { first, second }.Order(), new[] { NurseryPrivate(view, "_first"), NurseryPrivate(view, "_second") }.Cast<Guid>().Order());
            Assert.True(((Button)view.FindName("StartButton")!).IsEnabled);
            Assert.All(recipe.AvailableOutcomes, o => Assert.Contains(OutcomeLabels(view), t => t.Text.Contains($"№{o.SpeciesId} ·")));
            Assert.Contains(OutcomeLabels(view), t => t.Text.EndsWith('%'));
        });
    }

    [Fact]
    public void PairBookDimsAPairWhoseOnlyParentIsAwayAndHidesItFromNewPairs()
    {
        WpfTestHost.Run(() =>
        {
            var (data, _, second) = State();
            data.Expedition = new ExpeditionState { Status = "running", ParticipantIds = [second] };
            var view = View(data);
            // No pair can start a new species now, so the book opens on "Все пары" with the busy one dimmed.
            var row = Assert.Single(PairRows(view));
            Assert.False(row.IsEnabled);
            Assert.Contains("В походе", ((NurseryPairOption)row.Tag).Busy);
            ((Button)view.FindName("NewPairsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Empty(PairRows(view));
        });
    }

    [Fact]
    public void PairBookPrefersACaringParentAndKeepsAHandPickedOne()
    {
        WpfTestHost.Run(() =>
        {
            var (data, first, second) = State();
            var caring = Individual(data.Individuals[0].CurrentSpeciesId); caring.FirstTraitId = "caring-parent"; data.Individuals.Add(caring);
            var view = View(data);
            PairRows(view)[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Contains(caring.Id, new[] { NurseryPrivate(view, "_first"), NurseryPrivate(view, "_second") });
            Choose(view, first, second);
            PairRows(view)[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(first, NurseryPrivate(view, "_first"));
            Assert.Equal(second, NurseryPrivate(view, "_second"));
        });
    }

    [Fact]
    public void CartBuysExactlyWhatIsMissingInOneSaveAndThenTheNestCanStart()
    {
        WpfTestHost.Run(() =>
        {
            var (data, first, second) = State();
            data.Berries = data.Berries.ToDictionary(x => x.Key, _ => 1);
            data.ShopCoins = 1000;
            var calls = new List<IReadOnlyList<(string Item, int Quantity, Guid Operation)>>();
            var view = View(data, buy: items =>
            {
                calls.Add(items);
                var shop = new ShopService(Catalog.Value.DataRoot);
                foreach (var (item, quantity, operation) in items) Assert.True(shop.TryPurchase(data, item, quantity, operation).Success);
                return Task.CompletedTask;
            });
            PairRows(view)[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var price = new NurseryService(Catalog.Value, data).Preview([first, second]).Berries;
            Assert.False(((Button)view.FindName("StartButton")!).IsEnabled);
            ((Button)view.FindName("ShopButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(Visibility.Visible, ((Border)view.FindName("CartPanel")!).Visibility);
            ((Button)view.FindName("CartBuyButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var bought = Assert.Single(calls);
            Assert.Equal(price.ToDictionary(x => x.Key, x => x.Value - 1), bought.ToDictionary(x => x.Item, x => x.Quantity));
            Assert.All(price, x => Assert.Equal(x.Value, data.Berries[x.Key]));
            Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("CartPanel")!).Visibility);
            Assert.True(((Button)view.FindName("StartButton")!).IsEnabled);
        });
    }

    [Fact]
    public void CartRefusesToBuyWithoutEnoughCoinsAndSaysHowMuchIsMissing()
    {
        WpfTestHost.Run(() =>
        {
            var (data, _, _) = State();
            data.Berries = data.Berries.ToDictionary(x => x.Key, _ => 0);
            data.ShopCoins = 0;
            var view = View(data, buy: _ => throw new InvalidOperationException("must not buy"));
            PairRows(view)[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((Button)view.FindName("ShopButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(((Button)view.FindName("CartBuyButton")!).IsEnabled);
            Assert.Contains("Не хватает", ((TextBlock)view.FindName("CartTotalText")!).Text);
        });
    }

    [Fact]
    public void WaitingShowsThePossibleBabiesAndAgainPutsTheSameParentsBack()
    {
        WpfTestHost.Run(() =>
        {
            var recipe = new NurseryCatalog(Catalog.Value).MixedRecipes.First(r => r.IsAvailable && r.SelectionUseful);
            var (data, first, second) = State(recipe);
            var request = new NurseryRequest([first, second], null, Guid.NewGuid(), DateTimeOffset.UtcNow);
            var service = new NurseryService(Catalog.Value, data);
            service.Start(request);
            var view = View(data);
            Assert.Equal(Visibility.Visible, ((TextBlock)view.FindName("NestTerms")!).Visibility);
            Assert.Equal(recipe.AvailableOutcomes.Count, ((WrapPanel)view.FindName("NestOutcomePanel")!).Children.Count);
            data.Nest!.ReadyAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1); data.Nest.Status = "ready";
            service.Hatch(request.OperationId, Guid.NewGuid());
            view.Refresh();
            Assert.Equal(Visibility.Visible, ((TextBlock)view.FindName("ResultNewText")!).Visibility);
            ((Button)view.FindName("AgainButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(first, NurseryPrivate(view, "_first"));
            Assert.Equal(second, NurseryPrivate(view, "_second"));
            Assert.True(((Button)view.FindName("StartButton")!).IsEnabled);
        });
    }
}
