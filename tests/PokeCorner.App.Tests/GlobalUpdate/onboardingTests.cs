using System.Windows.Controls;
using System.Windows;
using PokeCorner.App.Onboarding;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Tests.GlobalUpdate;

public class onboardingTests
{
    private static readonly string DataRoot = Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")), "src/PokeCorner.App/bin", Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!.Name, "net8.0-windows/Data");
    private static readonly Lazy<CatalogPackage> Package = new(() => CatalogPackage.Load(DataRoot));
    private static SaveData EmptyMigrated(int coins = 9, int treats = 3) => SaveMigration.Migrate(new SaveData
    {
        Individuals = [], Settings = new(), ShopCoins = coins, FeedingTreats = treats
    }, LegacyLookup.Load(DataRoot), Package.Value);

    private static PokemonIndividual Starter(int id = 1)
    {
        var species = Package.Value.Species.Single(s => s.Id == id);
        var starter = new PokemonIndividual
        {
            SpeciesId = id, CurrentSpeciesId = id, FamilyRootId = species.FamilyRootId,
            EvolutionChainId = species.EvolutionChainId, ChainLength = 3, Nature = Nature.Hardy,
            IsShiny = false, CaughtAtUtc = DateTimeOffset.Parse("2020-01-02T03:04:05Z"),
            ReachedSpeciesIds = [id], AcquisitionSource = "starter"
        };
        SaveMigration.InitializeIndividual(starter, LegacyLookup.Load(DataRoot), Package.Value);
        return starter;
    }

    [Fact]
    public void ExistingCollectionWithoutActiveDoesNotRequestStarter()
    {
        var saved = new SaveData { Individuals = [new() { SpeciesId = 1, EvolutionChainId = 1, ChainLength = 3, Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow }], Settings = new(), StarterSelectionPending = true };
        Assert.False(FirstRunFlow.NeedsStarter(saved));
        Assert.Single(saved.Individuals);
        Assert.Null(saved.ActiveIndividualId);
    }

    [Fact]
    public void OldTutorialDoesNotSuppressGlobalUpdateTour()
    {
        var saved = new SaveData { Individuals = [], Settings = new(), Tutorial = new() { Status = "not-started" } };
        Assert.True(FirstRunFlow.NeedsTour(saved));
        saved.Tutorial.Status = "completed";
        Assert.False(FirstRunFlow.NeedsTour(saved));
    }

    [Fact]
    public void TourHasExactlySixApprovedSteps() => WpfTestHost.Run(() =>
    {
        var window = new OnboardingWindow();
        var dots = ((Panel)window.FindName("StepDotsPanel")!).Children;
        Assert.Equal(6, dots.Count);
        Assert.Contains("ШАГ 1 ИЗ 6", ((TextBlock)window.FindName("StepCounterText")!).Text);
    });

    [Fact]
    public void ExistingEmptyCollectionKeepsResourcesWhileNewGameGetsStarterPack()
    {
        var existing = EmptyMigrated();
        FirstRunFlow.CompleteStarter(existing, Starter(), false, Package.Value);
        Assert.Equal(9, existing.ShopCoins);
        Assert.Equal(3, existing.FeedingTreats);
        Assert.Single(existing.Individuals);
        Assert.Equal(2, existing.Gift.Gifts.Count);

        var fresh = EmptyMigrated();
        FirstRunFlow.CompleteStarter(fresh, Starter(), true, Package.Value);
        Assert.Equal(20, fresh.ShopCoins);
        Assert.Equal(5, fresh.FeedingTreats);
        Assert.Equal(2, fresh.Gift.Gifts.Count);
        Assert.Equal("global-update-compatible-pair-v1", fresh.Gift.GrantId);
        Assert.NotEqual(fresh.Gift.Gifts[0].Individual.SpeciesId, fresh.Gift.Gifts[1].Individual.SpeciesId);
        Assert.All(fresh.Gift.Gifts, g =>
        {
            var species = Package.Value.Species.Single(s => s.Id == g.Individual.SpeciesId);
            Assert.Equal(species.Id, species.FamilyRootId);
            Assert.Contains(species.Rarity, ["Common", "Uncommon"]);
            Assert.Equal("reserved", g.Status);
            Assert.DoesNotContain(g.Individual.Id, fresh.Individuals.Select(p => p.Id));
        });
    }

    [Fact]
    public void LegacyPendingFlagDoesNotConvertExistingEmptyProfileIntoNewGame()
    {
        var legacy = new SaveData
        {
            Individuals = [], Settings = new(), ShopCoins = 9, FeedingTreats = 3,
            StarterSelectionPending = true
        };
        var migrated = SaveMigration.Migrate(legacy, LegacyLookup.Load(DataRoot), Package.Value);
        Assert.False(migrated.StarterSelectionPending);
        FirstRunFlow.CompleteStarter(migrated, Starter(), false, Package.Value);
        Assert.Equal(9, migrated.ShopCoins);
        Assert.Equal(3, migrated.FeedingTreats);
    }

    [Fact]
    public void ExistingUnavailableIndividualsStayIntactAndGiftIsReservedOnlyOnce()
    {
        var next = EmptyMigrated();
        var unavailable = Package.Value.Species.First(s => !s.NormalAvailable);
        var pet = new PokemonIndividual
        {
            SpeciesId = unavailable.Id, CurrentSpeciesId = unavailable.Id, FamilyRootId = unavailable.FamilyRootId,
            EvolutionChainId = unavailable.EvolutionChainId, ChainLength = 1,
            Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.Parse("2020-01-02T03:04:05Z")
        };
        next.Individuals.Add(pet);
        var originalId = pet.Id;
        FirstRunFlow.RestoreCompanionAndGift(next, Package.Value);
        Assert.Null(next.ActiveIndividualId);
        Assert.Single(next.Individuals);
        Assert.Equal(originalId, next.Individuals[0].Id);
        Assert.Equal(2, next.Gift.Gifts.Count);
        var giftIds = next.Gift.Gifts.Select(g => g.Individual.Id).ToArray();
        FirstRunFlow.SetTourProgress(next, 3, "in-progress");
        FirstRunFlow.SetTourProgress(next, 3, "skipped");
        FirstRunFlow.RestoreCompanionAndGift(next, Package.Value);
        Assert.Equal(giftIds, next.Gift.Gifts.Select(g => g.Individual.Id));
        Assert.Equal(9, next.ShopCoins);
        Assert.Equal(3, next.FeedingTreats);
    }

    [Fact]
    public void ExistingAvailableIndividualIsSelectedWithoutMintingStarter()
    {
        var next = EmptyMigrated();
        var unsupportedShiny = Package.Value.Species.First(s => s.NormalAvailable && !s.ShinyAvailable);
        var unavailable = new PokemonIndividual
        {
            SpeciesId = unsupportedShiny.Id, CurrentSpeciesId = unsupportedShiny.Id, FamilyRootId = unsupportedShiny.FamilyRootId,
            EvolutionChainId = unsupportedShiny.EvolutionChainId, ChainLength = 1,
            Nature = Nature.Hardy, IsShiny = true, CaughtAtUtc = DateTimeOffset.Parse("2020-01-02T03:04:05Z")
        };
        var available = Starter();
        next.Individuals.AddRange([unavailable, available]);
        next.StarterSelectionPending = true;
        FirstRunFlow.RestoreCompanionAndGift(next, Package.Value);
        Assert.Equal(available.Id, next.ActiveIndividualId);
        Assert.Equal(2, next.Individuals.Count);
        Assert.Equal(unavailable.Id, next.Individuals[0].Id);
        Assert.False(next.StarterSelectionPending);
        Assert.Equal(available.CurrentSpeciesId, next.Gift.ExcludedSpeciesId);
    }

    [Fact]
    public async Task TourProgressCommitsAndReplayNeverTouchesGiftOrResources()
    {
        var root = Path.Combine(Path.GetTempPath(), "TourTest_" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "state-v2.json");
            var store = new JsonSaveStore(path);
            var data = EmptyMigrated();
            FirstRunFlow.CompleteStarter(data, Starter(), false, Package.Value);
            store.Save(data);
            var state = new AppState();
            state.Hydrate(data);
            var writer = new BackgroundSaveWriter(store);
            await state.CommitAsync(writer, next => FirstRunFlow.SetTourProgress(next, 4, "in-progress"), () => 0);
            Assert.Equal(4, store.Load()!.Tutorial.CurrentStep);
            await state.CommitAsync(writer, next => FirstRunFlow.SetTourProgress(next, 4, "skipped"), () => 0);
            await state.CommitAsync(writer, next => next.Tutorial.SeenHintKeys.Add("view:pokedex"), () => 0);
            await state.CommitAsync(writer, next => next.Tutorial.SeenHintKeys.Add("view:pokedex"), () => 0);
            var saved = store.Load()!;
            Assert.Equal("skipped", saved.Tutorial.Status);
            Assert.False(FirstRunFlow.NeedsTour(saved));
            Assert.Equal(data.Gift.Gifts.Select(g => g.Individual.Id), saved.Gift.Gifts.Select(g => g.Individual.Id));
            Assert.Equal(data.ShopCoins, saved.ShopCoins);
            Assert.Equal(data.FeedingTreats, saved.FeedingTreats);
            Assert.Equal(["view:pokedex"], saved.Tutorial.SeenHintKeys);
            writer.Stop();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ExistingNoActiveCommitsChosenOwnedPetAndGiftThenRestartDoesNotDuplicate()
    {
        var root = Path.Combine(Path.GetTempPath(), "ExistingCompanionTest_" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "state-v2.json");
            var store = new JsonSaveStore(path);
            var data = EmptyMigrated();
            var pet = Starter();
            data.Individuals.Add(pet);
            SaveMigration.EnsureGift(data, pet.CurrentSpeciesId, Package.Value);
            Assert.True(FirstRunFlow.HasUnqueuedGift(data));
            store.Save(data);
            var state = new AppState();
            state.Hydrate(store.Load()!);
            var writer = new BackgroundSaveWriter(store);
            await state.CommitAsync(writer, next => FirstRunFlow.RestoreCompanionAndGift(next, Package.Value), () => 0);
            var committed = store.Load()!;
            Assert.Equal(pet.Id, committed.ActiveIndividualId);
            Assert.Single(committed.Individuals);
            Assert.Equal(2, committed.Gift.Gifts.Count);
            Assert.Equal(2, committed.Encounters.Count(e => e.Source == "gift" && e.Phase == "sealed"));
            Assert.False(FirstRunFlow.HasUnqueuedGift(committed));
            var giftIds = committed.Gift.Gifts.Select(g => g.Individual.Id).ToArray();
            state.Hydrate(committed);
            await state.CommitAsync(writer, next => FirstRunFlow.RestoreCompanionAndGift(next, Package.Value), () => 0);
            Assert.Equal(giftIds, store.Load()!.Gift.Gifts.Select(g => g.Individual.Id));
            Assert.Equal(2, store.Load()!.Encounters.Count(e => e.Source == "gift"));
            Assert.Single(store.Load()!.Individuals);
            writer.Stop();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task StarterGiftAppearsAsTwoClosedReadyBallsAndOpensDurablyWithoutActivityCatch()
    {
        var root = Path.Combine(Path.GetTempPath(), "GiftCatchTest_" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "state-v2.json");
            var store = new JsonSaveStore(path);
            var state = new AppState();
            state.Hydrate(EmptyMigrated());
            var writer = new BackgroundSaveWriter(store);
            var service = new EncounterService(Package.Value);
            await state.CommitAsync(writer, next => FirstRunFlow.CompleteStarter(next, Starter(), true, Package.Value), () => 0);
            var saved = store.Load()!;
            Assert.Equal(2, saved.Encounters.Count(e => e.Source == "gift" && e.Phase == "sealed"));
            Assert.Equal(0, saved.PendingCatchCount);
            Assert.All(saved.Encounters, e =>
            {
                Assert.Equal("gift", e.Source);
                Assert.Null(e.Individual);
                Assert.Null(e.KnownSpeciesId);
                Assert.NotNull(e.GiftIndividualId);
            });
            WpfTestHost.Run(() =>
            {
                var view = new PokeCorner.App.Popup.CatchView(state, () => null,
                    _ => Task.FromResult<byte[]?>(null), () => 0, () => { }, _ => "hidden");
                Assert.True(((Button)view.FindName("CatchButton")!).IsEnabled);
                Assert.Contains("подар", ((TextBlock)view.FindName("StatusText")!).Text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(saved.Gift.Gifts[0].Individual.SpeciesId.ToString(), ((TextBlock)view.FindName("StatusText")!).Text);
            });

            var firstGift = CatchBallQueue.NextReady(state.Encounters)!;
            Assert.Equal("gift", firstGift.Source);
            Directory.CreateDirectory(path + ".tmp");
            var failingWriter = new BackgroundSaveWriter(store);
            await Assert.ThrowsAnyAsync<IOException>(() => state.CommitAsync(failingWriter,
                next => service.Open(next, firstGift.Id), () => 0));
            Assert.Equal("sealed", state.Encounters.Single(e => e.Id == firstGift.Id).Phase);
            Assert.Equal("sealed", store.Load()!.Encounters.Single(e => e.Id == firstGift.Id).Phase);
            failingWriter.Stop();
            Directory.Delete(path + ".tmp");

            var activityId = Guid.NewGuid();
            var activityReady = 0;
            await state.CommitAsync(writer, next =>
            {
                service.Reserve(next, "activity", activityId);
                next.ActivityNewGuaranteed = true;
            }, () => activityReady, next => activityReady = next.PendingCatchCount);
            Assert.Equal(3, CatchBallQueue.ReadyCount(state.Encounters, activityReady));

            for (var i = 0; i < 2; i++)
            {
                var closed = CatchBallQueue.NextReady(state.Encounters)!;
                Assert.Equal("gift", closed.Source);
                await state.CommitAsync(writer, next => service.Open(next, closed.Id), () => activityReady,
                    next => activityReady = next.PendingCatchCount);
                var opened = store.Load()!;
                Assert.Equal("opened", opened.Encounters.Single(e => e.Id == closed.Id).Phase);
                Assert.Equal("opened", opened.Gift.Gifts.Single(g => g.Individual.Id == closed.GiftIndividualId).Status);
                Assert.Equal(1, opened.PendingCatchCount);
                Assert.True(opened.ActivityNewGuaranteed);
                Assert.Equal("sealed", opened.Encounters.Single(e => e.Id == activityId).Phase);
                state.Hydrate(opened); // restart from confirmed disk state
                await state.CommitAsync(writer, next => service.Open(next, closed.Id), () => activityReady);
                Assert.Equal(opened.Individuals.Select(p => p.Id), store.Load()!.Individuals.Select(p => p.Id));
            }
            Assert.Equal(activityId, CatchBallQueue.NextReady(state.Encounters)!.Id);
            await state.CommitAsync(writer, next => FirstRunFlow.RestoreCompanionAndGift(next, Package.Value), () => activityReady);
            Assert.Equal(2, store.Load()!.Gift.Gifts.Count);
            Assert.Equal(2, store.Load()!.Encounters.Count(e => e.Source == "gift"));
            Assert.Equal(3, store.Load()!.Individuals.Count); // starter + pair, without replacing the active pet
            Assert.Equal(saved.ActiveIndividualId, store.Load()!.ActiveIndividualId);
            Assert.Equal(20, store.Load()!.ShopCoins);
            Assert.Equal(5, store.Load()!.FeedingTreats);
            writer.Stop();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void CatchViewOffersAlreadyReservedGiftWhenNoActivityBallIsReady() => WpfTestHost.Run(() =>
    {
        var saved = EmptyMigrated();
        FirstRunFlow.CompleteStarter(saved, Starter(), true, Package.Value);
        var gift = saved.Gift.Gifts[0];
        new EncounterService(Package.Value).ReserveGift(saved, gift.Individual.Id, gift.Individual.Id);
        var live = new AppState();
        live.Hydrate(saved);
        var view = new PokeCorner.App.Popup.CatchView(live, () => null,
            _ => Task.FromResult<byte[]?>(null), () => 0, () => { }, _ => "hidden");
        Assert.True(((Button)view.FindName("CatchButton")!).IsEnabled);
        Assert.Contains("подар", ((TextBlock)view.FindName("StatusText")!).Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("талисман не меняет", ((TextBlock)view.FindName("CatchCharmText")!).Text, StringComparison.OrdinalIgnoreCase);
    });

    [Fact]
    public async Task FailedStarterCommitDoesNotPublishCandidateOrGift()
    {
        var root = Path.Combine(Path.GetTempPath(), "StarterCommitTest_" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "state-v2.json");
            var store = new JsonSaveStore(path);
            var initial = EmptyMigrated();
            initial.StarterSelectionPending = true;
            store.Save(initial);
            var state = new AppState();
            state.Hydrate(initial);
            Directory.CreateDirectory(path + ".tmp");
            var writer = new BackgroundSaveWriter(store);
            await Assert.ThrowsAnyAsync<IOException>(() => state.CommitAsync(writer,
                next => FirstRunFlow.CompleteStarter(next, Starter(), true, Package.Value), () => 0));
            Assert.Empty(state.Collection.Individuals);
            Assert.Empty(state.ToSaveData(0).Gift.Gifts);
            Assert.True(store.Load()!.StarterSelectionPending);
            Assert.Empty(store.Load()!.Individuals);
            writer.Stop();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void TourCanAdvanceSkipAndReplayWithoutChangingOtherState()
    {
        var saved = EmptyMigrated();
        var before = saved.ShopCoins;
        FirstRunFlow.SetTourProgress(saved, 2, "in-progress");
        Assert.True(FirstRunFlow.NeedsTour(saved));
        FirstRunFlow.SetTourProgress(saved, 2, "skipped");
        Assert.False(FirstRunFlow.NeedsTour(saved));
        Assert.Equal(before, saved.ShopCoins);
        Assert.Empty(saved.Gift.Gifts);
        Assert.Throws<ArgumentOutOfRangeException>(() => FirstRunFlow.SetTourProgress(saved, 7, "in-progress"));
    }

    [Fact]
    public void ResumedWindowStartsAtSavedStepAndFitsCompactWidth() => WpfTestHost.Run(() =>
    {
        var window = new OnboardingWindow(4);
        WpfTestHost.Layout((FrameworkElement)window.Content, 360, 320);
        Assert.Contains("ШАГ 4 ИЗ 6", ((TextBlock)window.FindName("StepCounterText")!).Text);
        Assert.True(window.MinWidth <= 360);
        Assert.True(((FrameworkElement)window.Content).DesiredSize.Width <= 360);
    });

    [Fact]
    public void WindowNavigationSavesNextAndBackBeforeDisplayingStep() => WpfTestHost.Run(() =>
    {
        var saved = new List<(int Step, string Status)>();
        var window = new OnboardingWindow(2, (step, status) =>
        {
            saved.Add((step, status));
            return Task.CompletedTask;
        });
        ((Button)window.FindName("NextButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal((3, "in-progress"), saved.Single());
        Assert.Contains("ШАГ 3 ИЗ 6", ((TextBlock)window.FindName("StepCounterText")!).Text);
        ((Button)window.FindName("BackButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal((2, "in-progress"), saved.Last());
        Assert.Contains("ШАГ 2 ИЗ 6", ((TextBlock)window.FindName("StepCounterText")!).Text);
    });

    [Fact]
    public void EscapeClosesTourWithoutMarkingItSkipped() => WpfTestHost.Run(() =>
    {
        var saved = new List<(int, string)>();
        var window = new OnboardingWindow(3, (step, status) =>
        {
            saved.Add((step, status));
            return Task.CompletedTask;
        });
        window.Show();
        var key = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(window), 0, System.Windows.Input.Key.Escape)
        { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
        window.RaiseEvent(key);
        Assert.True(key.Handled);
        Assert.False(window.IsVisible);
        Assert.Empty(saved);
    });

    [Fact]
    public void ClosingDuringPendingStepCommitKeepsDisplayedStepUntilRestart()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        OnboardingWindow? window = null;
        string? beforeClose = null;
        WpfTestHost.Run(() =>
        {
            window = new OnboardingWindow(2, (_, _) => pending.Task);
            window.Show();
            ((Button)window.FindName("NextButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            beforeClose = ((TextBlock)window.FindName("StepCounterText")!).Text;
            window.Close();
        });
        pending.SetResult();
        WpfTestHost.Run(() =>
        {
            Assert.False(window!.IsVisible);
            Assert.Equal(beforeClose, ((TextBlock)window.FindName("StepCounterText")!).Text);
        });
    }
}
