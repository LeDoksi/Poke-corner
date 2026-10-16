using System.Windows.Controls;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Tests.GlobalUpdate;

public sealed class ExpeditionUiTests
{
    private static readonly Lazy<CatalogPackage> Catalog = new(() => CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data")));
    private static SaveData State(int count = 1)
    {
        var species = Catalog.Value.Species.First(s => Catalog.Value.CanIssueDirectly(s.Id, false));
        return new SaveData
        {
            SchemaVersion = 2, CatalogRevision = Catalog.Value.CatalogRevision, BalanceRevision = "test",
            MigrationId = SaveMigration.MigrationId, Settings = new(),
            Berries = SaveMigration.BerryIds.ToDictionary(id => id, _ => 0),
            Items = SaveMigration.ItemIds.ToDictionary(id => id, _ => 0),
            BerryRemainders = SaveMigration.BerryIds.ToDictionary(id => id, _ => new RationalRemainder()),
            Individuals = Enumerable.Range(0, count).Select(_ => new PokemonIndividual
            {
                SpeciesId = species.Id, CurrentSpeciesId = species.Id, FamilyRootId = species.Id,
                EvolutionChainId = species.EvolutionChainId, ChainLength = 3, ReachedSpeciesIds = [species.Id],
                Nature = Nature.Bold, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow,
                FirstTraitId = "gatherer"
            }).ToList()
        };
    }

    private static ExpeditionView View(SaveData data, Func<bool>? confirmCancel = null,
        Func<Guid, DateTimeOffset, Task<ExpeditionAction>>? cancel = null,
        Func<Guid, Guid, Task<ExpeditionAction>>? claim = null) =>
        new(() => data, request => new ExpeditionService(Catalog.Value, data).Preview(request),
            request => Task.FromResult(new ExpeditionService(Catalog.Value, data).Start(request)),
            cancel ?? ((id, now) => Task.FromResult(new ExpeditionService(Catalog.Value, data).Cancel(id, now))),
            claim ?? ((id, op) => Task.FromResult(new ExpeditionService(Catalog.Value, data).Claim(id, op))),
            _ => Task.FromResult(default(EncounterOpenResult)!), Catalog.Value, confirmCancel);

    [Fact]
    public void RunningPanelKeepsLandscapeTraitsAndNonExpiringRewardsVisible() => WpfTestHost.Run(() =>
    {
        var data = State();
        new ExpeditionService(Catalog.Value, data).Start(new ExpeditionRequest("grove", "easy", 30,
            [data.Individuals[0].Id], Guid.NewGuid(), DateTimeOffset.UtcNow));
        var view = View(data);
        WpfTestHost.Layout(view, 600, 600);
        var image = Assert.IsType<Image>(view.FindName("LandscapeImage"));
        var panel = (System.Windows.FrameworkElement)view.FindName("Landscape");
        Assert.NotSame(view.FindName("PreparationPanel"), panel.Parent);
        Assert.Contains("Собиратель", ((TextBlock)view.FindName("TripTeam")).Text);
        Assert.Contains("Лёгкая", ((TextBlock)view.FindName("TripTerms")).Text);
        Assert.Contains("не сгорают", ((TextBlock)view.FindName("TripRewards")).Text);
        Assert.Equal("Лесная роща", ((TextBlock)view.FindName("LandscapeName")).Text);
    });

    [Fact]
    public void ResultCoinLabelSaysExtrasAreIncludedInTotal() => WpfTestHost.Run(() =>
    {
        var data = State(3);
        var service = new ExpeditionService(Catalog.Value, data);
        var id = Guid.NewGuid();
        service.Start(new ExpeditionRequest("grove", "easy", 30, data.Individuals.Select(p => p.Id).ToArray(),
            id, DateTimeOffset.UtcNow.AddHours(-2)));
        data.Expedition!.Status = "ready";
        data.Expedition.Rewards.ExtraCoins = 7;
        service.Claim(id, Guid.NewGuid());
        var view = View(data);
        Assert.Contains("монеты всего (включая дополнительные)", ((TextBlock)view.FindName("ResultText")).Text);
    });
    [Fact]
    public void PreparationShowsEightRoutes()
    {
        WpfTestHost.Run(() =>
        {
            var data = new SaveData { SchemaVersion = 2, Individuals = [], Settings = new() };
            var view = new ExpeditionView(() => data, _ => new ExpeditionPreview("rawst", 3, 60, 90, 0, 0, DateTimeOffset.UtcNow, new Dictionary<Guid, int>()),
                _ => Task.FromResult(new ExpeditionAction(true, "ok")), (_, _) => Task.FromResult(new ExpeditionAction(true, "ok")),
                (_, _) => Task.FromResult(new ExpeditionAction(true, "ok")), _ => Task.FromResult(default(EncounterOpenResult)!));
            WpfTestHost.Layout(view, 600, 420);
            Assert.Equal(8, view.RouteButtons.Children.OfType<Button>().Count());
            Assert.True(view.DesiredSize.Width <= 600);
        });
    }

    [Fact]
    public void SelectedGuidUsesCanonicalPreviewAndNestMemberIsUnavailable()
    {
        WpfTestHost.Run(() =>
        {
            var data = State(2);
            data.Nest = new NurseryState { Status = "running", ParticipantIds = [data.Individuals[1].Id], Child = data.Individuals[1] };
            var view = View(data);
            WpfTestHost.Layout(view, 600, 420);
            var team = (WrapPanel)view.FindName("TeamPanel")!;
            var buttons = team.Children.OfType<Button>().ToArray();
            Assert.True(buttons[0].IsEnabled);
            Assert.False(buttons[1].IsEnabled);
            buttons[0].RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            var expected = new ExpeditionService(Catalog.Value, data).Preview(new ExpeditionRequest("grove", "normal", 90,
                [data.Individuals[0].Id], Guid.NewGuid(), DateTimeOffset.UtcNow));
            var shown = ((TextBlock)view.FindName("PreviewText")!).Text;
            Assert.Contains($"{expected.PrimaryBerries} {expected.PrimaryBerryId}", shown);
            Assert.Contains($"{expected.Coins} монет", shown);
            Assert.Contains($"{expected.XpPerParticipant} XP", shown);
            Assert.Contains("−20%", ((TextBlock)view.FindName("ContributionsText")!).Text);
        });
    }

    [Fact]
    public void RunningTermsStayFrozenAndCancelBoundaryOffersClaim()
    {
        WpfTestHost.Run(() =>
        {
            var data = State();
            new ExpeditionService(Catalog.Value, data).Start(new ExpeditionRequest("grove", "normal", 90,
                [data.Individuals[0].Id], Guid.NewGuid(), DateTimeOffset.UtcNow));
            var cancelCalls = 0;
            var view = View(data,
                () => { data.Expedition!.ReadyAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1); return true; },
                (_, _) => { cancelCalls++; return Task.FromResult(new ExpeditionAction(true, "unexpected")); });
            WpfTestHost.Layout(view, 600, 420);
            var before = ((TextBlock)view.FindName("TripRewards")!).Text;
            data.Individuals[0].TotalXp = 100_000;
            view.Refresh();
            Assert.Equal(before, ((TextBlock)view.FindName("TripRewards")!).Text);
            ((Button)view.FindName("CancelButton")!).RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(0, cancelCalls);
            Assert.Equal(System.Windows.Visibility.Visible, ((Button)view.FindName("ClaimButton")!).Visibility);
        });
    }

    [Fact]
    public void FailedClaimKeepsReadyTripAndShowsError()
    {
        WpfTestHost.Run(() =>
        {
            var data = State();
            new ExpeditionService(Catalog.Value, data).Start(new ExpeditionRequest("grove", "easy", 30,
                [data.Individuals[0].Id], Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-2)));
            data.Expedition!.Status = "ready";
            var view = View(data, claim: (_, _) => Task.FromException<ExpeditionAction>(new IOException("write failed")));
            WpfTestHost.Layout(view, 600, 420);
            ((Button)view.FindName("ClaimButton")!).RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("ready", data.Expedition.Status);
            Assert.Contains("write failed", ((TextBlock)view.FindName("MessageText")!).Text);
            Assert.Equal(System.Windows.Visibility.Visible, ((Button)view.FindName("ClaimButton")!).Visibility);
        });
    }

    [Fact]
    public void ClaimRetryKeepsOperationIdUntilConfirmed()
    {
        WpfTestHost.Run(() =>
        {
            var data = State();
            new ExpeditionService(Catalog.Value, data).Start(new ExpeditionRequest("grove", "easy", 30,
                [data.Individuals[0].Id], Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-2)));
            data.Expedition!.Status = "ready";
            var ids = new List<Guid>();
            var view = View(data, claim: (_, id) =>
            {
                ids.Add(id);
                return ids.Count == 1
                    ? Task.FromException<ExpeditionAction>(new IOException("write failed"))
                    : Task.FromResult(new ExpeditionAction(true, "saved"));
            });
            var claimButton = (Button)view.FindName("ClaimButton")!;
            claimButton.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            claimButton.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(2, ids.Count);
            Assert.Equal(ids[0], ids[1]);
        });
    }

    [Fact]
    public void FailedStartKeepsSelectedTeamAndStableRequest()
    {
        WpfTestHost.Run(() =>
        {
            var data = State();
            var sent = new List<ExpeditionRequest>();
            var view = new ExpeditionView(() => data, request => new ExpeditionService(Catalog.Value, data).Preview(request),
                request =>
                {
                    sent.Add(request);
                    return Task.FromException<ExpeditionAction>(new IOException("write failed"));
                },
                (_, _) => Task.FromResult(new ExpeditionAction(true, "cancelled")),
                (_, _) => Task.FromResult(new ExpeditionAction(true, "claimed")),
                _ => Task.FromResult(default(EncounterOpenResult)!), Catalog.Value);
            var team = (WrapPanel)view.FindName("TeamPanel")!;
            team.Children.OfType<Button>().Single().RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            var start = (Button)view.FindName("StartButton")!;
            start.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            start.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(2, sent.Count);
            Assert.Equal(sent[0], sent[1]);
            Assert.Null(data.Expedition);
            Assert.Equal(System.Windows.Visibility.Visible, ((StackPanel)view.FindName("PreparationPanel")!).Visibility);
            Assert.Contains("write failed", ((TextBlock)view.FindName("MessageText")!).Text);
        });
    }

    [Fact]
    public void ClaimedClosedEncounterOpensByItsSavedId()
    {
        WpfTestHost.Run(() =>
        {
            var data = State(3);
            var request = new ExpeditionRequest("grove", "normal", 30,
                data.Individuals.Select(p => p.Id).ToArray(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-2));
            var service = new ExpeditionService(Catalog.Value, data);
            service.Start(request);
            data.Expedition!.Rewards.HasEncounter = true;
            data.Expedition.Status = "ready";
            var result = service.Claim(data.Expedition.Id, Guid.NewGuid());
            Assert.True(result.Encounter!.IsClosed);
            var openedId = Guid.Empty;
            var view = new ExpeditionView(() => data, query => new ExpeditionService(Catalog.Value, data).Preview(query),
                _ => Task.FromResult(new ExpeditionAction(true, "started")),
                (_, _) => Task.FromResult(new ExpeditionAction(true, "cancelled")),
                (_, _) => Task.FromResult(new ExpeditionAction(true, "claimed")),
                id =>
                {
                    openedId = id;
                    return Task.FromResult(new EncounterService(Catalog.Value).Open(data, id, DateTimeOffset.UtcNow));
                }, Catalog.Value);
            var openButton = (Button)view.FindName("OpenEncounterButton")!;
            Assert.Equal(System.Windows.Visibility.Visible, openButton.Visibility);
            openButton.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(result.Encounter.Id, openedId);
            Assert.Equal("opened", data.Encounters.Single(e => e.Id == openedId).Phase);
            Assert.Equal(System.Windows.Visibility.Collapsed, openButton.Visibility);
        });
    }

    [Fact]
    public void FailedOpenCanRetryInSameView()
    {
        WpfTestHost.Run(() =>
        {
            var data = State(3);
            var request = new ExpeditionRequest("grove", "normal", 30,
                data.Individuals.Select(p => p.Id).ToArray(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-2));
            var service = new ExpeditionService(Catalog.Value, data);
            service.Start(request);
            data.Expedition!.Rewards.HasEncounter = true;
            data.Expedition.Status = "ready";
            service.Claim(request.OperationId, Guid.NewGuid());
            var calls = 0;
            var view = new ExpeditionView(() => data, query => new ExpeditionService(Catalog.Value, data).Preview(query),
                _ => Task.FromResult(new ExpeditionAction(true, "started")),
                (_, _) => Task.FromResult(new ExpeditionAction(true, "cancelled")),
                (_, _) => Task.FromResult(new ExpeditionAction(true, "claimed")),
                id =>
                {
                    calls++;
                    return calls == 1
                        ? Task.FromException<EncounterOpenResult>(new IOException("write failed"))
                        : Task.FromResult(new EncounterService(Catalog.Value).Open(data, id));
                }, Catalog.Value);
            var button = (Button)view.FindName("OpenEncounterButton")!;
            button.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            Assert.True(button.IsEnabled);
            Assert.Equal("resolved", data.Encounters.Single().Phase);
            button.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(2, calls);
            Assert.Equal("opened", data.Encounters.Single().Phase);
            Assert.Equal(4, data.Individuals.Count);
        });
    }

    [Fact]
    public void InFlightStartCannotChangeVisibleRequestOrRetryIdentity()
    {
        WpfTestHost.Run(() =>
        {
            var data = State();
            var sent = new List<ExpeditionRequest>();
            ExpeditionView? view = null;
            view = new ExpeditionView(() => data, request => new ExpeditionService(Catalog.Value, data).Preview(request),
                request =>
                {
                    sent.Add(request);
                    if (sent.Count == 1)
                    {
                        view!.RouteButtons.Children.OfType<Button>().Last().RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                        ((ComboBox)view.FindName("DurationBox")!).SelectedIndex = 3;
                        return Task.FromException<ExpeditionAction>(new IOException("write failed"));
                    }
                    return Task.FromResult(new ExpeditionAction(true, "saved"));
                },
                (_, _) => Task.FromResult(new ExpeditionAction(true, "cancelled")),
                (_, _) => Task.FromResult(new ExpeditionAction(true, "claimed")),
                _ => Task.FromResult(default(EncounterOpenResult)!), Catalog.Value);
            ((WrapPanel)view.FindName("TeamPanel")!).Children.OfType<Button>().Single()
                .RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            var start = (Button)view.FindName("StartButton")!;
            start.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("Лесная роща", ((TextBlock)view.FindName("LandscapeName")!).Text);
            Assert.Equal(1, ((ComboBox)view.FindName("DurationBox")!).SelectedIndex);
            start.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(sent[0], sent[1]);
        });
    }

    [Fact]
    public void ReloadedClaimedViewShowsExactPersistedReceipt()
    {
        WpfTestHost.Run(() =>
        {
            var data = State();
            var request = new ExpeditionRequest("grove", "easy", 30,
                [data.Individuals[0].Id], Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-2));
            var service = new ExpeditionService(Catalog.Value, data);
            service.Start(request);
            data.Expedition!.Status = "ready";
            service.Claim(request.OperationId, Guid.NewGuid());
            var loaded = JsonSaveStore.Clone(data);
            var view = View(loaded);
            var text = ((TextBlock)view.FindName("ResultText")!).Text;
            Assert.Contains($"×{loaded.Expedition!.Receipt!.PrimaryBerries}", text);
            Assert.Contains($"монеты всего (включая дополнительные): {loaded.Expedition.Receipt.Coins}", text);
            Assert.Contains($"+{loaded.Expedition.Receipt.XpByParticipant[request.ParticipantIds[0]]} XP", text);
            loaded.Expedition.Receipt = null;
            view.Refresh();
            Assert.Contains("не сохранены", ((TextBlock)view.FindName("ResultText")!).Text);
        });
    }
}
