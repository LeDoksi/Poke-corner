using System.Windows;
using System.Windows.Controls;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Tests.GlobalUpdate;

public sealed class NurseryUiTests
{
    private static readonly Lazy<CatalogPackage> Catalog = new(() => CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data")));

    private static (SaveData Data, Guid First, Guid Second) State()
    {
        var recipe = new NurseryCatalog(Catalog.Value).MixedRecipes.First(r => r.IsAvailable && r.SelectionUseful);
        var data = new SaveData
        {
            SchemaVersion = 2, CatalogRevision = Catalog.Value.CatalogRevision, BalanceRevision = "test",
            MigrationId = SaveMigration.MigrationId, Settings = new(),
            Berries = SaveMigration.BerryIds.ToDictionary(id => id, _ => 100),
            Items = SaveMigration.ItemIds.ToDictionary(id => id, _ => 3),
            BerryRemainders = SaveMigration.BerryIds.ToDictionary(id => id, _ => new RationalRemainder()),
            Individuals = [Individual(recipe.FirstParentId), Individual(recipe.SecondParentId)]
        };
        return (data, data.Individuals[0].Id, data.Individuals[1].Id);
    }

    private static PokemonIndividual Individual(int speciesId)
    {
        var species = Catalog.Value.Species.Single(s => s.Id == speciesId);
        return new PokemonIndividual
        {
            SpeciesId = speciesId, CurrentSpeciesId = speciesId, FamilyRootId = species.FamilyRootId,
            EvolutionChainId = species.EvolutionChainId, ChainLength = 1, ReachedSpeciesIds = [speciesId],
            Nature = Nature.Bold, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow, FirstTraitId = "gatherer"
        };
    }

    private static NurseryView View(SaveData data, Func<NurseryRequest, Task<NurseryAction>>? start = null,
        Func<Guid, Guid, Task<NurseryAction>>? hatch = null, Action<string, int>? shortage = null,
        Action<Guid>? openPokedex = null, Func<Guid, Task>? activate = null) =>
        new(() => data, (ids, selected) => new NurseryService(Catalog.Value, data).Preview(ids, selected),
            start ?? (request => Task.FromResult(new NurseryService(Catalog.Value, data).Start(request))),
            hatch ?? ((id, op) => Task.FromResult(new NurseryService(Catalog.Value, data).Hatch(id, op))),
            now => Task.FromResult(new NurseryService(Catalog.Value, data).RefreshReady(now)), Catalog.Value,
            shortage ?? ((_, _) => { }), openPokedex ?? (_ => { }),
            activate ?? (id => { data.ActiveIndividualId = id; return Task.CompletedTask; }));

    private static void Choose(NurseryView view, Guid first, Guid second)
    {
        ((WrapPanel)view.FindName("FirstParentsPanel")!).Children.OfType<Button>().Single(b => Equals(b.Tag, first))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ((WrapPanel)view.FindName("SecondParentsPanel")!).Children.OfType<Button>().Single(b => Equals(b.Tag, second))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    [Fact]
    public void CompactScreenShowsDistinctCompatibleParentsAndCanonicalPreview()
    {
        WpfTestHost.Run(() =>
        {
            var (data, first, second) = State();
            var view = View(data);
            WpfTestHost.Layout(view, 600, 420);
            Assert.True(view.DesiredSize.Width <= 600);
            Choose(view, first, second);
            Assert.DoesNotContain(((WrapPanel)view.FindName("SecondParentsPanel")!).Children.OfType<Button>(), b => Equals(b.Tag, first));
            var expected = new NurseryService(Catalog.Value, data).Preview([first, second]);
            var cost = ((TextBlock)view.FindName("CostText")!).Text;
            Assert.Contains($"{expected.Hours} ч", cost);
            Assert.All(expected.Berries, berry => Assert.Contains($"{berry.Key} ×{berry.Value}", cost));
            Assert.All(((WrapPanel)view.FindName("OutcomePanel")!).Children.OfType<Button>(),
                button => { Assert.True(button.Focusable); Assert.True(button.IsTabStop); });
            Assert.True(((Button)view.FindName("StartButton")!).IsTabStop);
        });
    }

    [Fact]
    public void BusyParentRefusedAndShortageNavigationDoesNotBuy()
    {
        WpfTestHost.Run(() =>
        {
            var (data, first, second) = State();
            data.Expedition = new ExpeditionState { Status = "running", ParticipantIds = [second] };
            var view = View(data);
            Assert.False(((WrapPanel)view.FindName("FirstParentsPanel")!).Children.OfType<Button>()
                .Single(b => Equals(b.Tag, second)).IsEnabled);
            data.Expedition = null;
            data.Berries = data.Berries.ToDictionary(x => x.Key, _ => 0);
            var opened = (Berry: "", Required: 0);
            view = View(data, shortage: (berry, required) => opened = (berry, required));
            Choose(view, first, second);
            var expected = new NurseryService(Catalog.Value, data).Preview([first, second]);
            ((Button)view.FindName("ShopButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(expected.Berries.First().Key, opened.Berry);
            Assert.Equal(expected.Berries.First().Value, opened.Required);
            Assert.All(data.Berries, x => Assert.Equal(0, x.Value));
            Assert.Null(data.Nest);
        });
    }

    [Fact]
    public void ChosenOutcomeRequiresTalismanAndShopOnlyPrefillsIt()
    {
        WpfTestHost.Run(() =>
        {
            var (data, first, second) = State();
            data.Items["offspring-charm"] = 0;
            var opened = (Good: "", Required: 0);
            var view = View(data, shortage: (good, required) => opened = (good, required));
            Choose(view, first, second);
            ((WrapPanel)view.FindName("OutcomePanel")!).Children.OfType<Button>().Skip(1).First()
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(((Button)view.FindName("StartButton")!).IsEnabled);
            ((Button)view.FindName("ShopButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(("offspring-charm", 1), opened);
            Assert.Equal(0, data.Items["offspring-charm"]);
            Assert.Null(data.Nest);
        });
    }

    [Fact]
    public void FailedStartRetriesSameRequestAndKeepsChildHidden()
    {
        WpfTestHost.Run(() =>
        {
            var (data, first, second) = State();
            var sent = new List<NurseryRequest>();
            var view = View(data, start: request =>
            {
                sent.Add(request);
                return sent.Count == 1
                    ? Task.FromException<NurseryAction>(new IOException("write failed"))
                    : Task.FromResult(new NurseryService(Catalog.Value, data).Start(request));
            });
            Choose(view, first, second);
            var button = (Button)view.FindName("StartButton")!;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Null(data.Nest);
            Assert.Contains("write failed", ((TextBlock)view.FindName("MessageText")!).Text);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(sent[0], sent[1]);
            Assert.Equal("running", data.Nest!.Status);
            Assert.Contains("скрыт", ((TextBlock)view.FindName("NestTerms")!).Text);
            Assert.Equal(Visibility.Collapsed, ((Button)view.FindName("HatchButton")!).Visibility);
        });
    }

    [Fact]
    public void FailedHatchRetainsReadyNestAndRetriesSameOperation()
    {
        WpfTestHost.Run(() =>
        {
            var (data, first, second) = State();
            var request = new NurseryRequest([first, second], null, Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-9));
            new NurseryService(Catalog.Value, data).Start(request);
            data.Nest!.Status = "ready";
            var sent = new List<Guid>();
            var view = View(data, hatch: (id, op) =>
            {
                sent.Add(op);
                return sent.Count == 1
                    ? Task.FromException<NurseryAction>(new IOException("write failed"))
                    : Task.FromResult(new NurseryService(Catalog.Value, data).Hatch(id, op));
            });
            var button = (Button)view.FindName("HatchButton")!;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("ready", data.Nest.Status);
            Assert.Empty(data.HatchedNurseryResults);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(sent[0], sent[1]);
            Assert.Equal("hatched", data.Nest.Status);
            Assert.Contains(data.Nest.Child.SpeciesId.ToString(), ((TextBlock)view.FindName("ResultText")!).Text);
            Assert.Single(data.HatchedNurseryResults);
        });
    }

    [Fact]
    public void PendingStartRejectsReentrantSelectionAndUnconfirmedSuccess()
    {
        WpfTestHost.Run(() =>
        {
            var (data, first, second) = State();
            var sent = new List<NurseryRequest>();
            NurseryView? view = null;
            view = View(data, start: request =>
            {
                sent.Add(request);
                ((WrapPanel)view!.FindName("FirstParentsPanel")!).Children.OfType<Button>().Last()
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                return Task.FromResult(new NurseryAction(true, "ok"));
            });
            Choose(view, first, second);
            var button = (Button)view.FindName("StartButton")!;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Null(data.Nest);
            Assert.Contains("не подтверждён", ((TextBlock)view.FindName("MessageText")!).Text);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(sent[0], sent[1]);
        });
    }

    [Fact]
    public void HatchedResultOpensItsCardAndOnlyChangesActiveOnExplicitAction()
    {
        WpfTestHost.Run(() =>
        {
            var (data, first, second) = State();
            var request = new NurseryRequest([first, second], null, Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-9));
            var service = new NurseryService(Catalog.Value, data);
            service.Start(request);
            service.Hatch(request.OperationId, Guid.NewGuid());
            var opened = Guid.Empty;
            var view = View(data, openPokedex: id => opened = id);
            Assert.Null(data.ActiveIndividualId);
            ((Button)view.FindName("OpenPokedexButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(data.Nest!.Child.Id, opened);
            Assert.Null(data.ActiveIndividualId);
            ((Button)view.FindName("ActivateButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(data.Nest.Child.Id, data.ActiveIndividualId);
        });
    }
}
