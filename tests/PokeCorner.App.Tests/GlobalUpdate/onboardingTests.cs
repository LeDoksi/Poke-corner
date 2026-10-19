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
    public async Task ClosingDuringPendingStepCommitKeepsDisplayedStepUntilRestart()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        OnboardingWindow? window = null;
        string? beforeClose = null;
        Task? move = null;
        WpfTestHost.Run(() =>
        {
            window = new OnboardingWindow(2, (_, _) => pending.Task);
            window.Show();
            move = (Task)typeof(OnboardingWindow).GetMethod("MoveAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(window, [2, "in-progress"])!;
            beforeClose = ((TextBlock)window.FindName("StepCounterText")!).Text;
            window.Close();
        });
        pending.SetResult();
        await move!; // The actual move has completed before the closed window is inspected.
        WpfTestHost.Run(() =>
        {
            Assert.False(window!.IsVisible);
            Assert.Equal(beforeClose, ((TextBlock)window.FindName("StepCounterText")!).Text);
        });
    }

    [Fact]
    public async Task RepeatedTourActionWhileCommitPendingOnlySavesOnce()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        Task? moving = null;
        OnboardingWindow? window = null;
        WpfTestHost.Run(() =>
        {
            window = new OnboardingWindow(1, (_, _) => { calls++; return pending.Task; });
            var method = typeof(OnboardingWindow).GetMethod("MoveAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            moving = (Task)method.Invoke(window, [1, "in-progress"])!;
            _ = (Task)method.Invoke(window, [1, "in-progress"])!;
            Assert.Equal(1, calls);
        });
        pending.SetResult();
        await moving!;
        WpfTestHost.Run(() => window!.Close());
    }

    [Theory]
    [InlineData(false, false, 1, false)]
    [InlineData(true, false, 2, false)]
    [InlineData(true, true, 2, true)]
    public async Task ResetRequiresBothDeliberateConfirmations(bool first, bool second, int expectedPrompts, bool reset)
    {
        var calls = 0; var prompts = 0; Task? action = null;
        WpfTestHost.Run(() =>
        {
            var view = new SettingsView(new SettingsViewModel(new AppSettings()), () => { }, () => calls++,
                confirmAsync: (_, _, _) => Task.FromResult(++prompts == 1 ? first : second),
                getAutostart: () => false, setAutostart: _ => throw new Exception("OS must not be touched"));
            action = (Task)typeof(SettingsView).GetMethod("ResetAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(view, [])!;
        });
        await action!;
        Assert.Equal(expectedPrompts, prompts); Assert.Equal(reset ? 1 : 0, calls);
    }

    [Fact]
    public async Task ResetCannotContinueAfterSettingsUnloads()
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0; var prompts = 0; Task? action = null;
        WpfTestHost.Run(() =>
        {
            var view = new SettingsView(new SettingsViewModel(new AppSettings()), () => { }, () => calls++,
                confirmAsync: (_, _, _) => { prompts++; return pending.Task; }, getAutostart: () => false);
            var method = typeof(SettingsView).GetMethod("ResetAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            action = (Task)method.Invoke(view, [])!; _ = (Task)method.Invoke(view, [])!;
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
        pending.SetResult(true); await action!;
        Assert.Equal(1, prompts); Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task HistorySelectionRequiresTwoConfirmationsAndUsesExactCopy(bool first, bool second)
    {
        var entry = new SaveHistoryEntry("synthetic-private-path", DateTimeOffset.UtcNow, 2, 123, 45);
        var prompts = 0; Task? selection = null; SaveHistoryWindow? window = null;
        WpfTestHost.Run(() =>
        {
            window = new SaveHistoryWindow([entry], 1, (_, body, _) =>
            {
                Assert.Contains("123", body); Assert.DoesNotContain(entry.Path, body);
                return Task.FromResult(++prompts == 1 ? first : second);
            });
            ((ListBox)window.FindName("HistoryList")).SelectedIndex = 0;
            selection = (Task)typeof(SaveHistoryWindow).GetMethod("SelectAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, [])!;
        });
        await selection!;
        WpfTestHost.Run(() => { Assert.Equal(first && second ? entry : null, window!.SelectedEntry); window.Close(); });
        Assert.Equal(first ? 2 : 1, prompts);
    }

    [Fact]
    public async Task ClosingHistoryDuringPendingConfirmationKeepsSelectionEmpty()
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        SaveHistoryWindow? window = null; Task? selection = null; var prompts = 0;
        WpfTestHost.Run(() =>
        {
            window = new SaveHistoryWindow([new("synthetic", DateTimeOffset.UtcNow, 2, 10, 2)], 0,
                (_, _, _) => { prompts++; return pending.Task; });
            ((ListBox)window.FindName("HistoryList")).SelectedIndex = 0;
            selection = (Task)typeof(SaveHistoryWindow).GetMethod("SelectAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, [])!;
            window.Close();
        });
        pending.SetResult(true); await selection!;
        Assert.Equal(1, prompts); Assert.Null(window!.SelectedEntry);
    }

    [Fact]
    public void SettingsAutostartSeamNeverNeedsRealRegistryAndFailureIsActionable() => WpfTestHost.Run(() =>
    {
        var writes = new List<bool>();
        var view = new SettingsView(new SettingsViewModel(new AppSettings()), () => { },
            getAutostart: () => false, setAutostart: enabled => { writes.Add(enabled); throw new IOException("synthetic registry denial"); });
        var check = (CheckBox)view.FindName("AutostartCheck");
        Assert.False(check.IsChecked); Assert.Empty(writes);
        check.IsChecked = true;
        Assert.Equal([true], writes); Assert.False(check.IsChecked);
        Assert.Contains("synthetic registry denial", ((TextBlock)view.FindName("FeedbackText")).Text);
    });

    [Fact]
    public async Task HostedHistoryKeepsMenuUsableAndCancelsPendingConfirmation()
    {
        AppMenuWindow? menu = null; SaveHistoryWindow? history = null; Task? hosted = null; Task? selection = null;
        WpfTestHost.Run(() =>
        {
            menu = ShellTests.CreateMenu(new AppState());
            menu.Navigate(MenuSection.Settings);
            history = new SaveHistoryWindow([new("synthetic", DateTimeOffset.UtcNow, 2, 8, 20)], 0, menu.ConfirmAsync);
            hosted = menu.ShowHostedAsync(history.TakeHostedContent());
            Assert.True(menu.IsEnabled);
            Assert.Equal(Visibility.Visible, ((ContentControl)menu.FindName("HostedContent")).Visibility);
            ((ListBox)history.FindName("HistoryList")).SelectedIndex = 0;
            selection = (Task)typeof(SaveHistoryWindow).GetMethod("SelectAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(history, [])!;
            Assert.Equal(Visibility.Visible, ((ConfirmationOverlay)menu.FindName("ConfirmationHost")).Visibility);
            menu.CompleteHostedContent();
        });
        await Task.WhenAll(hosted!, selection!);
        WpfTestHost.Run(() =>
        {
            Assert.Null(history!.SelectedEntry);
            Assert.True(menu!.IsEnabled);
            Assert.Equal(Visibility.Visible, ((ContentControl)menu.FindName("SettingsContent")).Visibility);
            history.Close(); menu.Close();
        });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void SixIllustratedStepsShowCommittedGiftQueueWithoutGranting(int step) => WpfTestHost.Run(() =>
    {
        var window = new OnboardingWindow(step, dataRoot: DataRoot, giftQueueCount: () => 1);
        WpfTestHost.Layout((FrameworkElement)window.Content, 360, 320);
        var illustration = (StackPanel)window.FindName("Illustration");
        Assert.NotEmpty(illustration.Children.OfType<System.Windows.Controls.Image>());
        Assert.Contains($"ШАГ {step} ИЗ 6", ((TextBlock)window.FindName("StepCounterText")).Text);
        if (step == 6) Assert.Contains("в очереди: 1", ((TextBlock)window.FindName("BodyText")).Text);
        window.Close();
    });

    [Fact]
    public async Task TourSaveFailureRemainsOnCurrentStepAndAllowsRetry()
    {
        OnboardingWindow? window = null; Task? moving = null; var calls = 0;
        WpfTestHost.Run(() =>
        {
            window = new OnboardingWindow(2, (_, _) => ++calls == 1 ? Task.FromException(new IOException("synthetic save refusal")) : Task.CompletedTask);
            moving = (Task)typeof(OnboardingWindow).GetMethod("MoveAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, [2, "in-progress"])!;
        });
        await moving!;
        WpfTestHost.Run(() =>
        {
            Assert.Contains("ШАГ 2", ((TextBlock)window!.FindName("StepCounterText")).Text);
            Assert.Contains("synthetic save refusal", ((TextBlock)window.FindName("ErrorText")).Text);
            moving = (Task)typeof(OnboardingWindow).GetMethod("MoveAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, [2, "in-progress"])!;
        });
        await moving!;
        WpfTestHost.Run(() => { Assert.Contains("ШАГ 3", ((TextBlock)window!.FindName("StepCounterText")).Text); window.Close(); });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task StarterFallsBackOnlyWhenCanonicalFramesUnavailable(int canonicalCount)
    {
        var chains = LegacyLookup.Load(DataRoot).Chains.Where(c => Package.Value.CanIssueDirectly(c.StarterSpeciesId, false)).Take(1).ToArray();
        var frame = GifFrameDecoder.DecodeFramesAsPng(File.ReadAllBytes(Package.Value.GetAssetPath(chains[0].StarterSpeciesId, false)!))[0];
        var legacyCalls = 0; StarterSelectionWindow? window = null; Task? loading = null;
        WpfTestHost.Run(() =>
        {
            window = new StarterSelectionWindow(chains, _ => { legacyCalls++; return Task.FromResult<byte[]?>(frame); },
                _ => Task.FromResult<IReadOnlyList<byte[]>?>(Enumerable.Repeat(frame, canonicalCount).ToArray()));
            loading = (Task)typeof(StarterSelectionWindow).GetMethod("LoadSpritesAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, [])!;
        });
        await loading!;
        WpfTestHost.Run(() => { Assert.NotNull(((System.Windows.Controls.Image)window!.FindName("Candidate1Image")).Source); window.Close(); });
        Assert.Equal(canonicalCount == 0 ? 1 : 0, legacyCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingStarterFramesPreserveNullAndFailureFallbackSemantics(bool fail)
    {
        var chains = LegacyLookup.Load(DataRoot).Chains.Where(c => Package.Value.CanIssueDirectly(c.StarterSpeciesId, false)).Take(1).ToArray();
        StarterSelectionWindow? window = null; Task? loading = null; var calls = 0;
        WpfTestHost.Run(() =>
        {
            window = new StarterSelectionWindow(chains, _ =>
            {
                calls++;
                return fail ? Task.FromException<byte[]?>(new IOException("synthetic legacy failure")) : Task.FromResult<byte[]?>(null);
            }, _ => Task.FromResult<IReadOnlyList<byte[]>?>(null));
            loading = (Task)typeof(StarterSelectionWindow).GetMethod("LoadSpritesAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, [])!;
        });
        if (fail) await Assert.ThrowsAsync<IOException>(() => loading!);
        else await loading!;
        WpfTestHost.Run(() => { Assert.Null(((System.Windows.Controls.Image)window!.FindName("Candidate1Image")).Source); window.Close(); });
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ClosingStarterDuringFrameFetchNeverRequestsFallbackOrStartsAnimator()
    {
        var chains = LegacyLookup.Load(DataRoot).Chains.Where(c => Package.Value.CanIssueDirectly(c.StarterSpeciesId, false)).Take(1).ToArray();
        var pending = new TaskCompletionSource<IReadOnlyList<byte[]>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        StarterSelectionWindow? window = null; Task? loading = null; var calls = 0;
        WpfTestHost.Run(() =>
        {
            window = new StarterSelectionWindow(chains, _ => { calls++; return Task.FromResult<byte[]?>(null); }, _ => pending.Task);
            loading = (Task)typeof(StarterSelectionWindow).GetMethod("LoadSpritesAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, [])!;
            window.Close();
        });
        pending.SetResult(null); await loading!;
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ClosingSecondHistoryPromptCannotSelectOrRestore()
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        SaveHistoryWindow? window = null; Task? selection = null; var prompts = 0;
        WpfTestHost.Run(() =>
        {
            window = new SaveHistoryWindow([new("synthetic", DateTimeOffset.UtcNow, 2, 10, 2)], 0,
                (_, _, _) => ++prompts == 1 ? Task.FromResult(true) : pending.Task);
            ((ListBox)window.FindName("HistoryList")).SelectedIndex = 0;
            selection = (Task)typeof(SaveHistoryWindow).GetMethod("SelectAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, [])!;
            Assert.Equal(2, prompts); window.Close();
        });
        pending.SetResult(true); await selection!;
        Assert.Null(window!.SelectedEntry);
    }

    [Theory]
    [InlineData(1000, 760)]
    [InlineData(840, 650)]
    public void SettingsActionsAndHistoryRemainReachableAtAcceptedSizes(double width, double height) => WpfTestHost.Run(() =>
    {
        var settings = new SettingsView(new SettingsViewModel(new AppSettings()), () => { }, getAutostart: () => false);
        WpfTestHost.Layout(settings, width - 40, height - 140);
        var root = (Grid)settings.Content;
        var scroll = (ScrollViewer)root.Children[0];
        scroll.ScrollToEnd(); settings.UpdateLayout();
        foreach (var name in new[] { "RecoverDataButton", "ResetDataButton", "ShowOnboardingButton" })
        {
            var button = (Button)settings.FindName(name);
            var bounds = button.TransformToAncestor(scroll).TransformBounds(new Rect(0, 0, button.ActualWidth, button.ActualHeight));
            Assert.True(bounds.Left >= 0 && bounds.Right <= scroll.ActualWidth + 1);
            Assert.True(bounds.Top >= 0 && bounds.Bottom <= scroll.ActualHeight + 1);
        }
        var history = new SaveHistoryWindow(Enumerable.Range(0, 30).Select(i => new SaveHistoryEntry("private-" + i, DateTimeOffset.UtcNow.AddDays(-i), 2, i, i)).ToArray(), 2);
        try
        {
            WpfTestHost.Layout((FrameworkElement)history.Content, width - 40, height - 140);
            ((ListBox)history.FindName("HistoryList")).SelectedIndex = 29;
            Assert.True(((Button)history.FindName("RestoreButton")).IsEnabled);
            Assert.True(((FrameworkElement)history.Content).DesiredSize.Width <= width - 40);
        }
        finally { history.Close(); }
    });

    [Fact]
    public async Task OldHostedCompletionCannotCloseNewContent()
    {
        AppMenuWindow? menu = null; Task? first = null; Task? second = null;
        WpfTestHost.Run(() =>
        {
            menu = ShellTests.CreateMenu(new AppState());
            var old = new Grid(); var current = new Grid();
            first = menu.ShowHostedAsync(old);
            second = menu.ShowHostedAsync(current);
            menu.CompleteHostedContent(old);
            Assert.True(first.IsCompleted); Assert.False(second.IsCompleted);
            Assert.Same(current, ((ContentControl)menu.FindName("HostedContent")).Content);
            menu.CompleteHostedContent(current);
        });
        await Task.WhenAll(first!, second!);
        WpfTestHost.Run(() => menu!.Close());
    }
}
