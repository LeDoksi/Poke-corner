using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Tests.GlobalUpdate;

public sealed class CatchTests
{
    private static readonly CatchReveal Result = new(new CatchResult(1, 1, Nature.Bold, false, false, 3), RarityTier.Common);
    private static T Find<T>(CatchView view, string name) => Assert.IsType<T>(view.FindName(name));
    private static void Click(CatchView view, string name) => Find<Button>(view, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static CatchView View(AppState state, Func<Task<CatchReveal?>>? open = null, ManualDelay? clock = null,
        Func<Guid, Task<CatchReveal?>>? pinned = null, Func<Task>? charm = null, bool motion = true,
        Func<int, bool, Task<byte[]?>>? art = null, Action? changed = null, Action? back = null)
    {
        return new CatchView(state, null, _ => Task.FromResult<byte[]?>(null),
            () => state.Encounters.Count(e => e.Source == "activity" && e.Phase == "sealed"),
            changed ?? (() => { }), _ => "Бульбазавр", open ?? (() => Task.FromResult<CatchReveal?>(Result)),
            tryPerformEncounterAsync: pinned, activateLuckyCharmAsync: charm,
            animationEnabled: () => motion, delayAsync: clock is null ? null : clock.Wait, fetchVariantSprite: art,
            returnFromEncounter: back);
    }
    private static AppState State(string source = "activity")
    {
        var state = new AppState();
        state.Encounters.Add(new EncounterState { Id = Guid.NewGuid(), Source = source, Phase = source == "expedition" ? "resolved" : "sealed",
            KnownSpeciesId = source == "expedition" ? 1 : null,
            Individual = source == "expedition" ? new PokemonIndividual { SpeciesId = 1, EvolutionChainId = 1, ChainLength = 3, Nature = Nature.Bold, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow } : null,
            Rarity = source == "expedition" ? RarityTier.Common : null });
        return state;
    }

    [Theory]
    [InlineData("activity")]
    [InlineData("gift")]
    public void UnknownSourceStartsClosedWithoutSpeciesAccess(string source) => WpfTestHost.Run(() => {
        var calls = 0;
        var view = View(State(source), art: (_, _) => { calls++; return Task.FromResult<byte[]?>(null); });
        Assert.Equal("Кто внутри?", Find<TextBlock>(view, "EncounterTitleText").Text);
        Assert.Equal(Visibility.Collapsed, Find<TextBlock>(view, "RevealNameText").Visibility);
        Assert.Null(Find<System.Windows.Controls.Image>(view, "RevealSpriteImage").Source);
        Assert.Equal(0, calls);
        Assert.DoesNotContain("%", Find<TextBlock>(view, "CatchCharmText").Text);
    });

    [Fact]
    public void KnownExpeditionStartsVisibleWithoutGrant() => WpfTestHost.Run(() => {
        var opens = 0; var art = new List<(int, bool)>();
        var state = State("expedition");
        var view = View(state, pinned: _ => { opens++; return Task.FromResult<CatchReveal?>(Result); },
            art: (id, shiny) => { art.Add((id, shiny)); return Task.FromResult<byte[]?>(null); });
        Assert.Equal("Встреча с Бульбазавр", Find<TextBlock>(view, "EncounterTitleText").Text);
        Assert.Equal(Visibility.Visible, Find<TextBlock>(view, "RevealNameText").Visibility);
        Assert.Contains((1, false), art);
        Assert.Equal(0, opens);
        Assert.Empty(state.Collection.Individuals);
    });

    [Fact]
    public void ReadinessIncludesUnopenedResolvedExpedition()
    {
        var state = State("expedition");
        Assert.Equal(1, CatchBallQueue.ReadyCount(state.Encounters, 0));
        Assert.Equal(state.Encounters[0].Id, CatchBallQueue.NextReady(state.Encounters)?.Id);
        state.Encounters[0].Phase = "opened";
        Assert.Equal(0, CatchBallQueue.ReadyCount(state.Encounters, 0));
        Assert.Null(CatchBallQueue.NextReady(state.Encounters));
    }

    [Fact]
    public async Task EarnedBallOpensWithoutRevealingUntil1700Milliseconds()
    {
        var clock = new ManualDelay(); CatchView view = null!; Task playback = null!;
        WpfTestHost.Run(() => { view = View(State(), clock: clock); playback = view.PlayReveal(Result); });
        var phases = new[] { ("Кто-то шуршит внутри…", 500), ("Покебол покачивается…", 450), ("Покебол покачивается…", 450), ("Открывается!", 300) };
        try {
            for (var i = 0; i < phases.Length; i++) {
                await Until(() => clock.Durations.Count > i);
                WpfTestHost.Run(() => {
                    Assert.Equal(phases[i].Item1, Find<TextBlock>(view, "StatusText").Text);
                    Assert.Equal(Visibility.Collapsed, Find<TextBlock>(view, "RevealNameText").Visibility);
                    Assert.Null(Find<System.Windows.Controls.Image>(view, "RevealSpriteImage").Source);
                });
                Assert.Equal(phases[i].Item2, clock.Durations[i].TotalMilliseconds);
                clock.Complete(i);
            }
            await playback.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(1700, clock.Durations.Sum(d => d.TotalMilliseconds));
            WpfTestHost.Run(() => Assert.Contains("Бульбазавр", Find<TextBlock>(view, "RevealNameText").Text));
        }
        finally { WpfTestHost.Run(() => view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent))); }
    }

    [Fact]
    public async Task MetPokemonIsCaughtWithA3500MillisecondThrow()
    {
        var clock = new ManualDelay(); CatchView view = null!; Task playback = null!;
        WpfTestHost.Run(() => {
            view = View(State("expedition"), clock: clock);
            Assert.Equal("Бросить покебол", Find<Button>(view, "CatchButton").Content);
            playback = view.PlayReveal(Result);
        });
        var phases = new[] { ("Покебол летит…", 600), ("Покебол раскрылся…", 450), ("Мягкое приземление…", 450),
            ("Покебол покачивается…", 550), ("Покебол покачивается…", 550), ("Покебол покачивается…", 550), ("Пойман!", 350) };
        try {
            for (var i = 0; i < phases.Length; i++) {
                await Until(() => clock.Durations.Count > i);
                WpfTestHost.Run(() => Assert.Equal(phases[i].Item1, Find<TextBlock>(view, "StatusText").Text));
                Assert.Equal(phases[i].Item2, clock.Durations[i].TotalMilliseconds);
                clock.Complete(i);
            }
            await playback.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(3500, clock.Durations.Sum(d => d.TotalMilliseconds));
            WpfTestHost.Run(() => Assert.Equal(Visibility.Collapsed, Find<Grid>(view, "PokeBallCanvas").Visibility));
        }
        finally { WpfTestHost.Run(() => view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent))); }
    }

    [Fact]
    public async Task ReducedMotionUsesCalm250MillisecondReveal()
    {
        var clock = new ManualDelay(); CatchView view = null!; Task playback = null!;
        WpfTestHost.Run(() => { view = View(State(), clock: clock, motion: false); playback = view.PlayReveal(Result); });
        try {
            Assert.Equal(TimeSpan.FromMilliseconds(250), Assert.Single(clock.Durations));
            clock.Complete(0); await playback.WaitAsync(TimeSpan.FromSeconds(3));
            WpfTestHost.Run(() => Assert.Equal("Продолжить", Find<Button>(view, "CatchButton").Content));
        }
        finally { WpfTestHost.Run(() => view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent))); }
    }

    [Fact]
    public async Task RapidClickSkipReopenKeepsOneConfirmedResult()
    {
        var clock = new ManualDelay(); var opens = 0; CatchView view = null!;
        WpfTestHost.Run(() => {
            view = View(State(), () => { opens++; return Task.FromResult<CatchReveal?>(Result); }, clock);
            Click(view, "CatchButton"); Click(view, "CatchButton"); Click(view, "SkipButton");
            Assert.Equal("Продолжить", Find<Button>(view, "CatchButton").Content);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            var reopened = View(State(), () => { opens++; return Task.FromResult<CatchReveal?>(Result); });
            reopened.RestoreReveal(Result);
            Assert.Contains("Бульбазавр", Find<TextBlock>(reopened, "RevealNameText").Text);
        });
        await Until(() => opens == 1);
        Assert.Equal(1, opens);
    }

    [Fact]
    public async Task SaveRefusalRetriesThePinnedEncounterInsteadOfOldestGift()
    {
        var state = State("expedition"); var selected = state.Encounters[0].Id;
        state.Encounters.Insert(0, new EncounterState { Id = Guid.NewGuid(), Source = "gift" });
        var ids = new List<Guid>(); var clock = new ManualDelay(); CatchView view = null!;
        WpfTestHost.Run(() => {
            view = View(state, pinned: id => { ids.Add(id); return ids.Count == 1
                ? Task.FromException<CatchReveal?>(new IOException("synthetic refused write")) : Task.FromResult<CatchReveal?>(Result); }, clock: clock);
            Assert.True(view.SelectEncounter(selected));
            Click(view, "CatchButton");
        });
        await Until(() => ids.Count == 1);
        WpfTestHost.Run(() => {
            Assert.Contains("synthetic refused write", Find<TextBlock>(view, "StatusText").Text);
            Assert.Empty(state.Collection.Individuals);
            Click(view, "CatchButton");
        });
        await Until(() => ids.Count == 2);
        WpfTestHost.Run(() => view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)));
        Assert.Equal(new[] { selected, selected }, ids);
    }

    [Fact]
    public async Task ExpeditionEncounterResultLeadsBackToExpeditions()
    {
        var state = State("expedition"); var selected = state.Encounters[0].Id;
        var backs = 0; var opened = 0; var clock = new ManualDelay(); CatchView view = null!;
        WpfTestHost.Run(() => {
            view = View(state, pinned: _ => { opened++; return Task.FromResult<CatchReveal?>(Result); }, clock: clock, motion: false, back: () => backs++);
            Assert.True(view.SelectEncounter(selected));
            Click(view, "CatchButton");
        });
        await Until(() => opened == 1 && clock.Durations.Count == 1);
        clock.Complete(0);
        await Task.Delay(60);
        WpfTestHost.Run(() => {
            Assert.Equal("Вернуться к экспедициям", Find<Button>(view, "CatchButton").Content);
            Click(view, "CatchButton");
            Assert.Equal(1, backs);
        });
    }

    [Fact]
    public async Task NavigationDuringPendingCommitCannotAnimateHiddenScenario()
    {
        var commit = new TaskCompletionSource<CatchReveal?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = new ManualDelay(); CatchView view = null!; var calls = 0;
        WpfTestHost.Run(() => {
            view = View(State(), () => { calls++; return commit.Task; }, clock);
            Click(view, "CatchButton");
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Click(view, "CatchButton");
        });
        commit.SetResult(Result);
        await Until(() => calls == 1);
        await Task.Delay(60);
        WpfTestHost.Run(() => {
            Assert.Equal("Продолжить", Find<Button>(view, "CatchButton").Content);
            Assert.Empty(clock.Durations);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ContinueResetsCaptionsSceneAndResultArt() => WpfTestHost.Run(() => {
        var view = View(State());
        view.RestoreReveal(Result);
        Click(view, "CatchButton");
        Assert.Equal("Кто внутри?", Find<TextBlock>(view, "EncounterTitleText").Text);
        Assert.Equal(Visibility.Collapsed, Find<TextBlock>(view, "RevealNameText").Visibility);
        Assert.Equal("", Find<TextBlock>(view, "RevealNameText").Text);
        Assert.Null(Find<System.Windows.Controls.Image>(view, "RevealSpriteImage").Source);
        Assert.Equal(Visibility.Collapsed, Find<Button>(view, "SkipButton").Visibility);
    });

    [Fact]
    public async Task CharmStockChangesOnlyAfterConfirmedActivation()
    {
        var state = State(); state.Shop.Items["lucky-charm"] = 2;
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activations = 0; CatchView view = null!;
        WpfTestHost.Run(() => {
            view = View(state, charm: async () => { activations++; await done.Task; state.Shop.Items["lucky-charm"]--; state.Shop.CatchLuckyCharmActive = true; });
            Click(view, "CharmButton"); Click(view, "CharmButton");
            Assert.Equal(2, state.Shop.Items["lucky-charm"]);
            Assert.False(state.Shop.CatchLuckyCharmActive);
        });
        done.SetResult();
        await Until(() => state.Shop.CatchLuckyCharmActive);
        WpfTestHost.Run(() => {
            Assert.Contains("зарезервирован", Find<TextBlock>(view, "CatchCharmText").Text);
            Assert.Contains("1", Find<TextBlock>(view, "CatchCharmText").Text);
            Assert.False(Find<Button>(view, "CharmButton").IsEnabled);
        });
        Assert.Equal(1, activations);
    }


    [Fact]
    public void ExpeditionEntryCallbackNavigatesWithoutOpeningProducer() => WpfTestHost.Run(() => {
        var opens = 0; Guid? selected = null; var id = Guid.NewGuid();
        var view = new ExpeditionView(() => new SaveData { Settings = new(), Individuals = [] }, _ => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(),
            (_, _) => throw new InvalidOperationException(),
            _ => { opens++; throw new InvalidOperationException("Navigation must not open"); },
            enterEncounter: encounterId => selected = encounterId);
        var button = (Button)view.FindName("OpenEncounterButton");
        button.Tag = id;
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(id, selected);
        Assert.Equal(0, opens);
    });

    [Fact]
    public void UnavailableShinyNeverRequestsNormalOnlyArt() => WpfTestHost.Run(() => {
        var legacyCalls = 0;
        var view = new CatchView(new AppState(), () => null,
            _ => { legacyCalls++; return Task.FromResult<byte[]?>(null); }, () => 0, () => { }, _ => "Шайни");
        view.RestoreReveal(new CatchReveal(Result.Result with { IsShiny = true }, RarityTier.Common));
        Assert.Equal(0, legacyCalls);
        Assert.Null(Find<System.Windows.Controls.Image>(view, "RevealSpriteImage").Source);
        Assert.Contains("✨", Find<TextBlock>(view, "RevealNameText").Text);
    });

    [Fact]
    public async Task HiddenSpriteCompletionCannotOverwriteNextClosedScene()
    {
        var sprite = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CatchView view = null!;
        WpfTestHost.Run(() => {
            view = View(State(), art: (_, _) => sprite.Task);
            view.RestoreReveal(Result);
            Click(view, "CatchButton");
        });
        var catalog = CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data"));
        var image = File.ReadAllBytes(catalog.GetAssetPath(1, false)!);
        sprite.SetResult(image);
        await Task.Delay(40);
        WpfTestHost.Run(() => {
            Assert.Null(Find<System.Windows.Controls.Image>(view, "RevealSpriteImage").Source);
            Assert.Equal(Visibility.Collapsed, Find<TextBlock>(view, "RevealNameText").Visibility);
        });
    }

    [Fact]
    public void RepeatedLoadDetachesRefreshAndAnimationPreferenceOnUnload() => WpfTestHost.Run(() => {
        var view = View(State());
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var timerField = typeof(CatchView).GetField("_liveRefreshTimer", flags)!;
        var timer = timerField.GetValue(view);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Assert.Same(timer, timerField.GetValue(view));
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.Null(timerField.GetValue(view));
        Assert.False((bool)typeof(CatchView).GetField("_listening", flags)!.GetValue(view)!);
    });

    [Fact]
    public async Task RealConfirmedReceiptSurvivesPendingCommitNavigationWithoutSecondGrant()
    {
        var root = Path.Combine(Path.GetTempPath(), "catch-task5-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var catalog = CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data"));
            var service = new EncounterService(catalog);
            var data = SaveFixture(catalog);
            var activity = service.Reserve(data, "activity", Guid.NewGuid());
            var encounter = service.Reserve(data, "expedition", Guid.NewGuid(), 1);
            service.Resolve(data, encounter.Id);
            var live = new AppState(); live.Hydrate(data);
            var store = new JsonSaveStore(Path.Combine(root, "state.json"));
            store.Save(data);
            var writes = new List<Action>();
            var writer = new BackgroundSaveWriter(store, writes.Add);
            CatchReveal? confirmed = null; CatchView view = null!; CatchView? reopened = null;
            var ids = new List<Guid>(); var clock = new ManualDelay();
            async Task<CatchReveal?> Open(Guid id) {
                ids.Add(id);
                EncounterOpenResult? result = null;
                await live.CommitAsync(writer, next => result = service.Open(next, id),
                    () => 1, _ => confirmed = new CatchReveal(new CatchResult(result!.Individual.EvolutionChainId,
                        result.Individual.SpeciesId, result.Individual.Nature, result.Individual.IsShiny,
                        result.WasDuplicateSpecies, result.Individual.ChainLength), result.Rarity, id));
                return confirmed;
            }
            WpfTestHost.Run(() => {
                view = View(live, pinned: Open, clock: clock);
                Assert.True(view.SelectEncounter(encounter.Id));
                Click(view, "CatchButton"); Click(view, "CatchButton"); Click(view, "SkipButton");
                Assert.Single(writes);
                Assert.Empty(live.Collection.Individuals);
                Assert.Equal("resolved", live.Encounters.Single(e => e.Id == encounter.Id).Phase);
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                live.RunWhenReady(() => {
                    Assert.NotNull(confirmed);
                    reopened = View(live, pinned: Open);
                    reopened.RestoreReveal(confirmed!);
                });
                Assert.Null(reopened);
                writes[0]();
            });
            await Until(() => reopened is not null);
            WpfTestHost.Run(() => {
                Assert.Equal("Продолжить", Find<Button>(reopened!, "CatchButton").Content);
                Assert.Contains("Бульбазавр", Find<TextBlock>(reopened!, "RevealNameText").Text);
                Assert.Empty(clock.Durations);
                Assert.Single(live.Collection.Individuals);
            });
            var saved = store.Load()!;
            Assert.Single(saved.Individuals);
            Assert.Equal("sealed", saved.Encounters.Single(e => e.Id == activity.Id).Phase);
            var repeated = service.Open(saved, encounter.Id);
            Assert.True(repeated.Repeated);
            Assert.Equal(confirmed!.EncounterId, encounter.Id);
            Assert.Equal(confirmed.Result.SpeciesId, repeated.Individual.SpeciesId);
            Assert.Single(saved.Individuals);
            Assert.Equal(new[] { encounter.Id }, ids);
            writer.Stop();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RealWriteRefusalPreservesCharmAndPinnedQueueForSameOperationRetry()
    {
        var root = Path.Combine(Path.GetTempPath(), "catch-task5-refusal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var catalog = CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data"));
            var service = new EncounterService(catalog);
            var data = SaveFixture(catalog);
            data.ShopCatchLuckyCharmActive = true;
            var encounter = service.Reserve(data, "activity", Guid.NewGuid());
            var live = new AppState(); live.Hydrate(data);
            var store = new JsonSaveStore(Path.Combine(root, "state.json"));
            store.Save(data); var savedBytes = File.ReadAllBytes(Path.Combine(root, "state.json"));
            var writer = new BackgroundSaveWriter(store, _ => throw new IOException("synthetic refused write"));
            var ids = new List<Guid>(); CatchReveal? confirmed = null; CatchView view = null!;
            var timing = new System.Collections.Concurrent.ConcurrentQueue<string>();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var producerMayReturn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var handedOff = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<CatchReveal?> Open(Guid id) {
                ids.Add(id); EncounterOpenResult? result = null;
                await live.CommitAsync(writer, next => result = service.Open(next, id), () => 1,
                    _ => { timing.Enqueue($"afterPublish:{clock.ElapsedTicks}");
                    confirmed = new CatchReveal(new CatchResult(result!.Individual.EvolutionChainId,
                        result.Individual.SpeciesId, result.Individual.Nature, result.Individual.IsShiny,
                        result.WasDuplicateSpecies, result.Individual.ChainLength), result.Rarity, id); });
                await producerMayReturn.Task;
                timing.Enqueue($"producer-return:{clock.ElapsedTicks}");
                return confirmed;
            }
            WpfTestHost.Run(() => {
                view = View(live, pinned: Open, clock: new ManualDelay(), changed: () => {
                    timing.Enqueue($"view-handoff:{clock.ElapsedTicks}"); handedOff.TrySetResult();
                });
                Click(view, "CatchButton");
            });
            await Until(() => !live.IsCommitting);
            WpfTestHost.Run(() => {
                Assert.Contains("synthetic refused write", Find<TextBlock>(view, "StatusText").Text);
                Assert.Null(confirmed); Assert.Empty(live.Collection.Individuals);
                Assert.True(live.Shop.CatchLuckyCharmActive);
                Assert.Equal("sealed", Assert.Single(live.Encounters).Phase);
            });
            Assert.Equal(savedBytes, File.ReadAllBytes(Path.Combine(root, "state.json")));
            writer.Stop(); writer = new BackgroundSaveWriter(store);
            WpfTestHost.Run(() => Click(view, "CatchButton"));
            await Until(() => confirmed is not null);
            WpfTestHost.Run(() => {
                Assert.Null(typeof(CatchView).GetField("_pendingReveal", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view));
                timing.Enqueue($"published-before-view-handoff:{clock.ElapsedTicks}");
            });
            producerMayReturn.SetResult();
            await handedOff.Task.WaitAsync(TimeSpan.FromSeconds(5));
            WpfTestHost.Run(() => {
                var pending = typeof(CatchView).GetField("_pendingReveal", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view);
                timing.Enqueue($"skip:{clock.ElapsedTicks}:pending={pending is not null}");
                var diagnostics = Environment.GetEnvironmentVariable("POKE_SHELL_CAPTURE_DIR");
                if (!string.IsNullOrWhiteSpace(diagnostics))
                {
                    Directory.CreateDirectory(diagnostics);
                    File.AppendAllLines(Path.Combine(diagnostics, "catch-handoff.txt"), timing);
                }
                Click(view, "SkipButton");
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                Assert.Equal("Продолжить", Find<Button>(view, "CatchButton").Content);
                Assert.Single(live.Collection.Individuals);
            });
            Assert.Equal(new[] { encounter.Id, encounter.Id }, ids);
            Assert.Single(store.Load()!.Individuals);
            writer.Stop();
        }
        finally { Directory.Delete(root, true); }
    }

    private static SaveData SaveFixture(CatalogPackage catalog) => new() {
        SchemaVersion = 2, CatalogRevision = catalog.CatalogRevision, BalanceRevision = "test",
        MigrationId = SaveMigration.MigrationId,
        Individuals = [], Settings = new(),
        Berries = SaveMigration.BerryIds.ToDictionary(id => id, _ => 0),
        Items = SaveMigration.ItemIds.ToDictionary(id => id, _ => 0),
        BerryRemainders = SaveMigration.BerryIds.ToDictionary(id => id, _ => new RationalRemainder())
    };


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppCatchArtUsesCanonicalVariantBeforeLegacy(bool shiny)
    {
        var catalog = CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data"));
        var app = (global::PokeCorner.App.App)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(global::PokeCorner.App.App));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(global::PokeCorner.App.App).GetField("_catalogPackage", flags)!.SetValue(app, catalog);
        var fetch = typeof(global::PokeCorner.App.App).GetMethod("FetchCatchSpriteAsync", flags)!;
        var bytes = await (Task<byte[]?>)fetch.Invoke(app, [1, shiny])!;
        Assert.Equal(File.ReadAllBytes(catalog.GetAssetPath(1, shiny)!), bytes);
    }

    [Fact]
    public async Task AppUnavailableShinyNeverFallsBackToNormal()
    {
        var catalog = CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data"));
        var unsupported = catalog.Species.First(s => catalog.GetAssetPath(s.Id, false) is not null && catalog.GetAssetPath(s.Id, true) is null);
        var app = (global::PokeCorner.App.App)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(global::PokeCorner.App.App));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(global::PokeCorner.App.App).GetField("_catalogPackage", flags)!.SetValue(app, catalog);
        var bytes = await (Task<byte[]?>)typeof(global::PokeCorner.App.App).GetMethod("FetchCatchSpriteAsync", flags)!.Invoke(app, [unsupported.Id, true])!;
        Assert.Null(bytes);
    }

    [Fact]
    public void ConfirmedKnownThrowKeepsItsCaptionAfterProducerOpensEncounter() => WpfTestHost.Run(() => {
        var state = State("expedition"); var id = state.Encounters[0].Id;
        var clock = new ManualDelay();
        var view = View(state, clock: clock, pinned: selected => {
            Assert.Equal(id, selected);
            state.Encounters[0].Phase = "opened";
            return Task.FromResult<CatchReveal?>(Result with { EncounterId = id });
        });
        Click(view, "CatchButton");
        Assert.Equal("Ловим Бульбазавр", Find<TextBlock>(view, "EncounterTitleText").Text);
        Assert.DoesNotContain("Кто", Find<TextBlock>(view, "EncounterTitleText").Text);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    });

    private static async Task Until(Func<bool> predicate)
    {
        var limit = DateTime.UtcNow.AddSeconds(2);
        while (!predicate() && DateTime.UtcNow < limit) await Task.Delay(10);
        Assert.True(predicate(), "Expected catch transition did not occur");
    }

    public sealed class ManualDelay
    {
        public List<TimeSpan> Durations { get; } = [];
        private readonly List<TaskCompletionSource> _pending = [];
        public Task Wait(TimeSpan duration, CancellationToken cancellation)
        {
            Durations.Add(duration);
            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending.Add(source);
            cancellation.Register(() => source.TrySetCanceled(cancellation));
            return source.Task;
        }
        public void Complete(int index) => _pending[index].TrySetResult();
    }
}
