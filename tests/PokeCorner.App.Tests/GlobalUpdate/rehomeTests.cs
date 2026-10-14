using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;
using PokeCorner.App.Popup;
using PokeCorner.App.Imaging;
using PokeCorner.App.Tests.Theme;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Text.Json;

namespace PokeCorner.App.Tests.GlobalUpdate;

public sealed class RehomeTests
{
    private static readonly Lazy<CatalogPackage> Catalog = new(() => CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data")));

    private static SaveData State()
    {
        var species = Catalog.Value.Species.Single(s => s.Id == 1);
        PokemonIndividual Pet() => new()
        {
            SpeciesId = 1, CurrentSpeciesId = 1, FamilyRootId = 1, EvolutionChainId = species.EvolutionChainId,
            ChainLength = 3, ReachedSpeciesIds = [1], Nature = Nature.Hardy, IsShiny = false,
            CaughtAtUtc = DateTimeOffset.UtcNow, FirstTraitId = "gatherer"
        };
        var first = Pet();
        return new SaveData
        {
            SchemaVersion = 2, CatalogRevision = "test", BalanceRevision = "test", MigrationId = SaveMigration.MigrationId,
            Individuals = [first, Pet()], ActiveIndividualId = first.Id, Settings = new(), ShopCoins = 10,
            Berries = SaveMigration.BerryIds.ToDictionary(id => id, _ => 0),
            Items = SaveMigration.ItemIds.ToDictionary(id => id, _ => 0),
            BerryRemainders = SaveMigration.BerryIds.ToDictionary(id => id, _ => new RationalRemainder()),
            DiscoveredSpeciesIds = [1], ShinySpeciesIds = [1], AwardLedger = { ["legacy"] = new AwardEntry { CoinsGranted = 3 } }
        };
    }

    [Fact]
    public void QuoteUsesPackagedBalanceAndRequiresExplicitActiveReplacement()
    {
        var data = State();
        var target = data.Individuals[0].Id;
        var replacement = data.Individuals[1].Id;
        var service = new RehomingService(Catalog.Value, data);
        Assert.Contains("замен", service.Quote(target).Reason!);
        var quote = service.Quote(target, replacement);
        Assert.Null(quote.Reason);
        Assert.Equal(12, quote.Coins);
        Assert.False(service.Transfer(target, Guid.NewGuid()).Success);
        Assert.Equal(2, data.Individuals.Count);
        Assert.Equal(10, data.ShopCoins);
    }

    [Fact]
    public void TransferRemovesOnlyChosenGuidAndReplayCannotPayTwice()
    {
        var data = State();
        var target = data.Individuals[0].Id;
        var replacement = data.Individuals[1].Id;
        var service = new RehomingService(Catalog.Value, data);
        var operation = Guid.NewGuid();
        Assert.True(service.Transfer(target, operation, replacement).Success);
        Assert.Single(data.Individuals);
        Assert.Equal(replacement, data.Individuals[0].Id);
        Assert.Equal(replacement, data.ActiveIndividualId);
        Assert.Equal(22, data.ShopCoins);
        Assert.Equal(1, data.DiscoveredSpeciesIds.Single());
        Assert.Contains(1, data.ShinySpeciesIds);
        Assert.Contains("legacy", data.AwardLedger.Keys);
        Assert.True(service.Transfer(target, operation, replacement).Repeated);
        Assert.Equal(22, data.ShopCoins);
        Assert.False(service.Transfer(replacement, operation).Success);
        Assert.Equal(22, data.ShopCoins);
    }

    [Fact]
    public void FavoriteBusyAndLastIndividualAreProtected()
    {
        var data = State();
        var target = data.Individuals[1];
        var service = new RehomingService(Catalog.Value, data);
        target.IsFavorite = true;
        Assert.Contains("избран", service.Quote(target.Id).Reason!);
        target.IsFavorite = false;
        data.Nest = new NurseryState { Status = "ready", ParticipantIds = [target.Id], Child = target };
        Assert.Contains("занят", service.Quote(target.Id).Reason!);
        data.Nest = null;
        data.Expedition = new ExpeditionState { Status = "running", ParticipantIds = [target.Id] };
        Assert.Contains("занят", service.Quote(target.Id).Reason!);
        data.Expedition = null;
        data.Individuals.RemoveAt(0);
        Assert.Contains("последн", service.Quote(target.Id).Reason!, StringComparison.OrdinalIgnoreCase);
        Assert.False(service.Transfer(target.Id, Guid.NewGuid()).Success);
    }

    [Fact]
    public void MaximumQuoteIsFortyNineAndBusyReplacementIsRejected()
    {
        var data = State();
        var legendary = Catalog.Value.Species.First(s => s.Rarity.Equals("Legendary", StringComparison.OrdinalIgnoreCase)
            && s.Id == s.FamilyRootId);
        var maxXp = new LevelProgression(Catalog.Value.DataRoot).XpForLevel(40);
        var target = new PokemonIndividual
        {
            SpeciesId = legendary.Id, CurrentSpeciesId = legendary.Id, FamilyRootId = legendary.Id,
            EvolutionChainId = legendary.EvolutionChainId, ChainLength = 1, ReachedSpeciesIds = [legendary.Id],
            Nature = Nature.Hardy, IsShiny = true, CaughtAtUtc = DateTimeOffset.UtcNow,
            TotalXp = maxXp, FirstTraitId = "gatherer"
        };
        data.Individuals[1] = target;
        Assert.Equal(49, new RehomingService(Catalog.Value, data).Quote(target.Id).Coins);
        var active = data.Individuals[0].Id;
        data.Expedition = new ExpeditionState { Status = "ready", ParticipantIds = [target.Id] };
        Assert.Contains("замен", new RehomingService(Catalog.Value, data).Quote(active, target.Id).Reason!);
    }

    [Fact]
    public async Task FailedWriteKeepsPetAndCoinsThenRetrySucceeds()
    {
        var data = State();
        var app = new AppState();
        app.Hydrate(data);
        var id = data.Individuals[1].Id;
        var operation = Guid.NewGuid();
        var path = Path.Combine(Path.GetTempPath(), "rehome-test-" + Guid.NewGuid().ToString("N"), "state-v2.json");
        try
        {
            var store = new JsonSaveStore(path);
            await Assert.ThrowsAsync<IOException>(() => app.CommitAsync(
                new BackgroundSaveWriter(store, _ => throw new IOException("synthetic write failure")),
                next => Assert.True(new RehomingService(Catalog.Value, next).Transfer(id, operation).Success), () => 0));
            Assert.Equal(2, app.Collection.Individuals.Count);
            Assert.Equal(10, app.Shop.Coins);
            await app.CommitAsync(new BackgroundSaveWriter(store),
                next => Assert.True(new RehomingService(Catalog.Value, next).Transfer(id, operation).Success), () => 0);
            Assert.Single(app.Collection.Individuals);
            Assert.Equal(22, app.Shop.Coins);
        }
        finally { if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task OpenedEncounterRemainsHistoricalAfterDurableTransfer()
    {
        var data = State();
        var target = data.Individuals[1];
        data.Encounters.Add(new EncounterState
        {
            Id = Guid.NewGuid(), Source = "activity", Phase = "opened", Individual = target,
            Rarity = RarityTier.Uncommon
        });
        var live = new AppState();
        live.Hydrate(data);
        var path = Path.Combine(Path.GetTempPath(), "rehome-history-" + Guid.NewGuid().ToString("N"), "state-v2.json");
        try
        {
            var store = new JsonSaveStore(path);
            await live.CommitAsync(new BackgroundSaveWriter(store), next =>
                Assert.True(new RehomingService(Catalog.Value, next).Transfer(target.Id, Guid.NewGuid()).Success), () => 0);
            var loaded = store.Load()!;
            Assert.True(new RehomingService(Catalog.Value, loaded).Transfer(target.Id,
                loaded.RehomingResults.Single().Key).Repeated);
            Assert.Single(loaded.Individuals);
            Assert.Equal(target.Id, Assert.Single(loaded.Encounters).Individual!.Id);
            Assert.Contains(loaded.RehomingResults.Values, r => r.IndividualId == target.Id);
            Assert.Contains(1, loaded.DiscoveredSpeciesIds);
            Assert.Contains("legacy", loaded.AwardLedger.Keys);
        }
        finally { if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public void OpenedEncounterWithoutOwnedOrValidRehomeJournalIsRejected()
    {
        var data = State();
        var target = data.Individuals[1];
        data.Encounters.Add(new EncounterState
        {
            Id = Guid.NewGuid(), Source = "activity", Phase = "opened", Individual = target,
            Rarity = RarityTier.Uncommon
        });
        data.Individuals.Remove(target);
        Assert.Throws<JsonException>(() => JsonSaveStore.ValidateData(data));
        var operation = Guid.NewGuid();
        data.RehomingResults[operation] = new RehomeResult(target.Id, null, 12);
        data.CompletedOperations[$"rehome:{operation}"] = Guid.NewGuid();
        Assert.Throws<JsonException>(() => JsonSaveStore.ValidateData(data));
    }

    [Fact]
    public void RehomeCompletionRequiresMatchingResultAndCanonicalKey()
    {
        var data = State();
        data.CompletedOperations["shop:unrelated-operation"] = Guid.NewGuid();
        JsonSaveStore.ValidateData(data);

        foreach (var key in new[] { $"rehome:{Guid.NewGuid()}", "rehome", "rehome:", "rehome:not-a-guid",
                     $"rehome:{Guid.NewGuid():N}", $"rehome:{Guid.NewGuid()}:extra", $"REHOME:{Guid.NewGuid()}" })
        {
            data.CompletedOperations[key] = Guid.NewGuid();
            Assert.Throws<JsonException>(() => JsonSaveStore.ValidateData(data));
            data.CompletedOperations.Remove(key);
        }
    }

    [Fact]
    public void CardShowsProtectionAndCancelledConfirmationKeepsSelectedIndividual() => WpfTestHost.Run(() =>
    {
        var data = State();
        var live = new AppState();
        live.Hydrate(data);
        var id = data.Individuals[1].Id;
        var vm = new CollectionViewModel(live.Collection, live.CareStateByIndividualId, new CareModeService(),
            [], [], species: Catalog.Value.Species, discoveredSpeciesIds: () => live.DiscoveredSpeciesIds);
        var commits = 0;
        var view = new CollectionView(vm, live,
            new SpriteCache(Path.Combine(Path.GetTempPath(), "rehome-card", Guid.NewGuid().ToString("N"))),
            () => { }, () => { }, () => { }, () => null, () => false, () => false,
            previewRehome: individualId => new RehomingService(Catalog.Value, live.ToSaveData(0)).Quote(individualId),
            rehomeIndividual: (_, _) => { commits++; return Task.FromResult(new RehomeAction(true, "test")); });
        Assert.True(view.OpenIndividual(data.Individuals[0].Id));
        Assert.False(((Button)view.FindName("DetailRehomeButton")).IsEnabled);
        Assert.Contains("замен", ((TextBlock)view.FindName("DetailRehomeReasonText")).Text);
        Assert.True(view.OpenIndividual(id));
        WpfTestHost.Layout(view, 320, 420);
        Assert.True(((Border)view.FindName("DetailCard")).ActualWidth <= 320);
        Assert.Contains("12 монет", ((TextBlock)view.FindName("DetailRehomeReasonText")).Text);
        var opened = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) =>
        {
            var dialog = Application.Current.Windows.OfType<ConfirmDialog>().FirstOrDefault(x => x.IsVisible &&
                ((TextBlock)x.FindName("TitleText")).Text == "Найти новый дом?");
            if (dialog is null) return;
            opened = true;
            timer.Stop();
            dialog.Close();
        };
        timer.Start();
        ((Button)view.FindName("DetailRehomeButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        timer.Stop();
        Assert.True(opened);
        Assert.Equal(0, commits);
        Assert.Equal(id, view.SelectedIndividualId);
        Assert.Equal(2, live.Collection.Individuals.Count);
        Assert.Equal(10, live.Shop.Coins);
    });

    [Fact]
    public async Task DelayedTransferFailureDoesNotChangeAnotherOpenCard()
    {
        var data = State();
        var live = new AppState();
        live.Hydrate(data);
        var target = data.Individuals[1].Id;
        var other = data.Individuals[0].Id;
        var gate = new TaskCompletionSource<RehomeAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        CollectionView? view = null;
        WpfTestHost.Run(() =>
        {
            var vm = new CollectionViewModel(live.Collection, live.CareStateByIndividualId, new CareModeService(),
                [], [], species: Catalog.Value.Species, discoveredSpeciesIds: () => live.DiscoveredSpeciesIds);
            view = new CollectionView(vm, live,
                new SpriteCache(Path.Combine(Path.GetTempPath(), "rehome-delayed", Guid.NewGuid().ToString("N"))),
                () => { }, () => { }, () => { }, () => null, () => false, () => false,
                previewRehome: id => new RehomingService(Catalog.Value, live.ToSaveData(0)).Quote(id),
                rehomeIndividual: (_, _) => gate.Task);
            Assert.True(view.OpenIndividual(target));
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            timer.Tick += (_, _) =>
            {
                var dialog = Application.Current.Windows.OfType<ConfirmDialog>().FirstOrDefault(x => x.IsVisible &&
                    ((TextBlock)x.FindName("TitleText")).Text == "Найти новый дом?");
                if (dialog is null) return;
                timer.Stop();
                ((Button)dialog.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            };
            timer.Start();
            ((Button)view.FindName("DetailRehomeButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            timer.Stop();
            Assert.True(view.OpenIndividual(other));
        });
        gate.SetException(new IOException("delayed write failure"));
        await view!.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle).Task;
        WpfTestHost.Run(() =>
        {
            Assert.Equal(other, view.SelectedIndividualId);
            Assert.Equal(string.Empty, ((TextBlock)view.FindName("DetailErrorText")).Text);
            Assert.Equal(2, live.Collection.Individuals.Count);
        });
    }
}
