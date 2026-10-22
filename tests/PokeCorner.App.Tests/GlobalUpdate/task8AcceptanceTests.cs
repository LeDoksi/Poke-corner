using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using PokeCorner.App.Onboarding;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Tests.GlobalUpdate;

public class Task8AcceptanceTests
{
    [Theory]
    [InlineData("000000", false)]
    [InlineData("#000000", false)]
    [InlineData("#0001", true)]
    [InlineData("№0001", true)]
    public void ExpeditionNumericSearchRetainsPaddedIdsWithoutZeroWildcard(string query, bool found) => WpfTestHost.Run(() =>
    {
        var data = ApprovedExpeditionTests.State(1);
        data.Individuals[0].CurrentSpeciesId = 1;
        var view = ApprovedExpeditionTests.View(data);
        ApprovedExpeditionTests.Click(view, "ChooseTeamButton");
        ApprovedExpeditionTests.Find<TextBox>(view, "PickerSearchBox").Text = query;
        var rows = ApprovedExpeditionTests.Find<WrapPanel>(view, "PickerMembers");
        Assert.Equal(found, rows.Children.Count == 1);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    });

    [Fact]
    public void CompletedGiftTourUsesOpenedBallWithoutInventingAnotherGift() => WpfTestHost.Run(() =>
    {
        var window = new OnboardingWindow(6, giftQueueCount: () => 0);
        try
        {
            var illustration = (StackPanel)window.FindName("Illustration");
            var image = Assert.Single(illustration.Children.OfType<Image>());
            Assert.NotNull(image.Source);
            Assert.Contains("уже открыты", ((TextBlock)window.FindName("BodyText")).Text);
        }
        finally { window.Close(); }
    });

    [Fact]
    public async Task SharedArtProviderDoesNotRepresentRetainedUnsupportedShinyWithNormalArt()
    {
        var catalog = ApprovedExpeditionTests.Catalog.Value;
        var unsupported = catalog.Species.First(s => catalog.GetAssetPath(s.Id, false) is not null && catalog.GetAssetPath(s.Id, true) is null);
        var app = (global::PokeCorner.App.App)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(global::PokeCorner.App.App));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(global::PokeCorner.App.App).GetField("_catalogPackage", flags)!.SetValue(app, catalog);
        typeof(global::PokeCorner.App.App).GetField("_decodedFramesCache", flags)!.SetValue(app, new Dictionary<(int, bool), IReadOnlyList<byte[]>?>());
        var fetch = typeof(global::PokeCorner.App.App).GetMethod("GetAnimatedFramesAsync", flags, [typeof(int), typeof(bool)])!;
        Assert.Null(await (Task<IReadOnlyList<byte[]>?>)fetch.Invoke(app, [unsupported.Id, true])!);
    }

    [Fact]
    public async Task TwoSuccessfulSameItemPurchasesUseDistinctCommittedOperations()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Task8-shop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var catalog = ApprovedExpeditionTests.Catalog.Value;
        var data = SaveMigration.Migrate(new SaveData { Individuals = [], Settings = new() }, LegacyLookup.Load(catalog.DataRoot), catalog);
        data.ShopCoins = 100;
        var store = new JsonSaveStore(Path.Combine(directory, "state.json")); store.Save(data);
        var state = new AppState(); state.Hydrate(data);
        var writer = new BackgroundSaveWriter(store);
        ShopView view = null!;
        try
        {
            var shop = new ShopService(catalog.DataRoot);
            WpfTestHost.Run(() => {
                view = new ShopView(state, shop, update => state.CommitAsync(writer, update, () => 0),
                    () => { }, catalog, (_, _, _) => Task.FromResult(true));
                view.SelectBerryShortage("oran", 2);
            });
            for (var i = 0; i < 2; i++)
            {
                Task purchase = null!;
                WpfTestHost.Run(() => purchase = (Task)typeof(ShopView).GetMethod("Buy", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(view, [shop.Goods.Single(g => g.Id == "oran")])!);
                await purchase;
            }
            Assert.Equal(4, state.Shop.Berries["oran"]);
            Assert.Equal(2, store.Load()!.CompletedOperations.Count);
            Assert.Equal(80, state.Shop.Coins);
        }
        finally
        {
            WpfTestHost.Run(() => view?.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)));
            writer.Stop(); Directory.Delete(directory, true);
        }
    }
    [Fact]
    public void EveryNatureHasRussianPresentation()
    {
        Assert.All(Enum.GetValues<Nature>(), nature => {
            var label = NatureDisplay.Name(nature);
            Assert.NotEqual("неизвестный", label);
            Assert.All(label, letter => Assert.InRange(letter, 'а', 'ё'));
        });
    }

    [Fact]
    public void RetainedUnsupportedEvolvedShinyRoundTripsAndRemainsVisible()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Task8-owned-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var catalog = ApprovedExpeditionTests.Catalog.Value;
        var lookup = LegacyLookup.Load(catalog.DataRoot);
        var data = SaveMigration.Migrate(new SaveData { Individuals = [], Settings = new() }, lookup, catalog);
        var pet = new PokemonIndividual {
            SpeciesId = 688, CurrentSpeciesId = 689, FamilyRootId = 688,
            EvolutionChainId = catalog.Species.Single(x => x.Id == 688).EvolutionChainId,
            ChainLength = 2, CurrentStage = EvolutionStage.Stage2, IsShiny = true, Nature = Nature.Calm,
            CaughtAtUtc = DateTimeOffset.Parse("2025-01-01T00:00:00Z"), ReachedSpeciesIds = [688, 689],
            FirstTraitId = "gatherer", TotalXp = 150, Nickname = "Старый друг"
        };
        data.Individuals = [pet]; data.ActiveIndividualId = pet.Id; data.ShopCoins = 314;
        data.NormalSpeciesIds = [688]; data.ShinySpeciesIds = [688, 689]; data.DiscoveredSpeciesIds = [688, 689];
        data.EvolutionTransitions = ["688->689"];
        Assert.False(catalog.CanObtain(689, true));
        Assert.Null(catalog.GetAssetPath(689, true));
        var before = System.Text.Json.JsonSerializer.Serialize(pet);
        CollectionView? view = null;
        try
        {
            var store = new JsonSaveStore(Path.Combine(directory, "state.json"));
            store.Save(data); var loaded = store.Load()!; store.Save(loaded); loaded = store.Load()!;
            Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(Assert.Single(loaded.Individuals)));
            Assert.Equal(pet.Id, loaded.ActiveIndividualId); Assert.Equal(314, loaded.ShopCoins);
            Assert.Equal(data.NormalSpeciesIds.Order(), loaded.NormalSpeciesIds.Order());
            Assert.Equal(data.ShinySpeciesIds.Order(), loaded.ShinySpeciesIds.Order());
            Assert.Equal(data.DiscoveredSpeciesIds.Order(), loaded.DiscoveredSpeciesIds.Order());
            Assert.Equal(data.EvolutionTransitions, loaded.EvolutionTransitions);
            var state = new AppState(); state.Hydrate(loaded);
            WpfTestHost.Run(() => {
                var vm = new CollectionViewModel(state.Collection, state.CareStateByIndividualId, new CareModeService(),
                    lookup.Chains, state.PendingEggs, species: catalog.Species,
                    discoveredSpeciesIds: () => state.DiscoveredSpeciesIds,
                    normalSpeciesIds: () => state.NormalSpeciesIds, shinySpeciesIds: () => state.ShinySpeciesIds,
                    presentation: new CollectionPresentation(catalog));
                view = new CollectionView(vm, state, new SpriteCache(Path.Combine(directory, "sprites")),
                    () => { }, () => { }, () => { }, () => null, () => false, () => false,
                    getAnimatedFrames: (_, _) => Task.FromResult<IReadOnlyList<byte[]>?>(null),
                    getSpeciesAssetPath: catalog.GetAssetPath);
                WpfTestHost.Layout(view, 840, 650);
                var card = Assert.IsType<PokedexCard>(Assert.Single(((ItemsControl)view.FindName("PokedexGrid")).Items));
                Assert.Equal(pet.Id, card.IndividualId); Assert.Equal(689, card.SpeciesId); Assert.True(card.IsShiny);
                Assert.Null(card.SpriteImage);
                ((TextBox)view.FindName("PokedexSearchBox")).Text = "нет такого питомца";
                ((CheckBox)view.FindName("ShinyFilterToggle")).IsChecked = true;
                ((CheckBox)view.FindName("FavoritesFilterToggle")).IsChecked = true;
                Assert.Empty(((ItemsControl)view.FindName("PokedexGrid")).Items);
                ((Button)view.FindName("ResetIndividualFiltersButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Single(((ItemsControl)view.FindName("PokedexGrid")).Items);
                Assert.Empty(((TextBox)view.FindName("PokedexSearchBox")).Text);
                Assert.False(((CheckBox)view.FindName("ShinyFilterToggle")).IsChecked);
                Assert.False(((CheckBox)view.FindName("FavoritesFilterToggle")).IsChecked);
                Assert.True(view.OpenIndividual(pet.Id));
                Assert.Equal(Visibility.Visible, ((TextBlock)view.FindName("DetailSpritePlaceholder")).Visibility);
                Assert.Equal("Характер: спокойный", ((TextBlock)view.FindName("DetailNatureText")).Text);
            });
        }
        finally
        {
            WpfTestHost.Run(() => view?.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)));
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AssembledCloseReopenKeepsPendingOrUnacknowledgedCatchExactlyOnce(bool closeAfterCommit)
    {
        var directory = Path.Combine(Path.GetTempPath(), "Task8-assembled-catch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var catalog = ApprovedExpeditionTests.Catalog.Value;
        var data = SaveMigration.Migrate(new SaveData { Individuals = [], Settings = new() }, LegacyLookup.Load(catalog.DataRoot), catalog);
        data.ShopCatchLuckyCharmActive = true; data.PendingCatchCount = 2;
        var service = new EncounterService(catalog);
        var first = service.Reserve(data, "activity", Guid.NewGuid());
        var second = service.Reserve(data, "activity", Guid.NewGuid());
        var state = new AppState(); state.Hydrate(data);
        var store = new JsonSaveStore(Path.Combine(directory, "state.json")); store.Save(data);
        var writes = new List<Action>();
        var writer = new BackgroundSaveWriter(store, writes.Add);
        AppMenuWindow? menu = null; CatchView? current = null;
        CatchReveal? receipt = null;
        var producerReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reopened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var opens = 0; var acknowledgements = 0;
        async Task<CatchReveal?> Open(Guid id)
        {
            opens++;
            EncounterOpenResult? result = null;
            await state.CommitAsync(writer, next => result = service.Open(next, id), () => 2,
                _ => receipt = new CatchReveal(new CatchResult(result!.Individual.EvolutionChainId,
                    result.Individual.SpeciesId, result.Individual.Nature, result.Individual.IsShiny,
                    result.WasDuplicateSpecies, result.Individual.ChainLength), result.Rarity, id));
            return receipt;
        }
        void Click(FrameworkElement target, string name) =>
            ((Button)target.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        void OpenMenu()
        {
            if (state.IsCommitting) { state.RunWhenReady(OpenMenu); return; }
            var vm = new CollectionViewModel(state.Collection, state.CareStateByIndividualId, new CareModeService(), [], []);
            var collection = new CollectionView(vm, state, new SpriteCache(Path.Combine(directory, "sprites")),
                () => { }, () => { }, () => { }, () => null, () => false, () => false);
            current = new CatchView(state, null, _ => Task.FromResult<byte[]?>(null), () => 2,
                () => producerReturned.TrySetResult(), _ => "Питомец",
                onResultContinued: () => { receipt = null; acknowledgements++; }, tryPerformEncounterAsync: Open,
                delayAsync: (_, token) => Task.Delay(Timeout.InfiniteTimeSpan, token));
            if (receipt is not null) current.RestoreReveal(receipt);
            menu = new AppMenuWindow(collection, new SettingsView(new SettingsViewModel(state.Settings), () => { }),
                new ShopView(state, new ShopService(catalog.DataRoot), _ => Task.CompletedTask, () => { }, catalog),
                current, getSnapshot: () => state.ToSaveData(2));
            menu.Closed += (_, _) => menu = null;
            menu.Show(); menu.Navigate(MenuSection.Catch);
            reopened.TrySetResult();
        }
        try
        {
            WpfTestHost.Run(() => {
                OpenMenu(); reopened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Assert.Equal(first.Id, CatchBallQueue.NextReady(state.Encounters)!.Id);
                Click(current!, "CatchButton"); Assert.Single(writes);
                Assert.Empty(state.Collection.Individuals);
                if (!closeAfterCommit)
                {
                    Click(menu!, "CloseButton"); Assert.Null(menu);
                    OpenMenu(); Assert.Null(menu); Assert.False(reopened.Task.IsCompleted);
                }
                writes[0]();
            });
            await producerReturned.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (closeAfterCommit)
            {
                WpfTestHost.Run(() => {
                    Assert.NotNull(receipt);
                    Click(menu!, "CloseButton"); Assert.Null(menu);
                    OpenMenu();
                });
            }
            await reopened.Task.WaitAsync(TimeSpan.FromSeconds(5));
            WpfTestHost.Run(() => {
                Assert.Equal("Продолжить", ((Button)current!.FindName("CatchButton")).Content);
                Assert.Single(state.Collection.Individuals);
                Assert.False(state.Shop.CatchLuckyCharmActive);
                Assert.Equal("sealed", state.Encounters.Single(x => x.Id == second.Id).Phase);
                menu!.Navigate(MenuSection.Pokedex); menu.Navigate(MenuSection.Catch);
                Assert.Equal("Продолжить", ((Button)current.FindName("CatchButton")).Content);
                Click(current, "CatchButton"); Assert.Null(receipt);
            });
            Assert.Equal(1, opens); Assert.Equal(1, acknowledgements);
            var saved = store.Load()!;
            Assert.Single(saved.Individuals); Assert.False(saved.ShopCatchLuckyCharmActive);
            Assert.Equal("sealed", saved.Encounters.Single(x => x.Id == second.Id).Phase);
            Assert.Equal(first.Id, Assert.Single(saved.Individuals).Id);
            saved.ShopCatchLuckyCharmActive = true;
            var repeated = service.Open(saved, first.Id);
            Assert.True(repeated.Repeated); Assert.Single(saved.Individuals);
            Assert.True(saved.ShopCatchLuckyCharmActive); // Replay must not consume a later charm.
            Assert.Single(writes);
        }
        finally
        {
            WpfTestHost.Run(() => menu?.Close());
            writer.Stop(); Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void NativeInputUsesHouseTemplateAndKeepsSelectionEditing() => WpfTestHost.Run(() =>
    {
        var view = ApprovedExpeditionTests.View(ApprovedExpeditionTests.State(1));
        ApprovedExpeditionTests.Click(view, "ChooseTeamButton");
        WpfTestHost.Layout(view, 840, 540);
        var field = (TextBox)view.FindName("PickerSearchBox");
        Assert.NotNull(field.Template.FindName("PART_ContentHost", field));
        var hint = (TextBlock)field.Template.FindName("Hint", field);
        Assert.Equal("Имя, кличка или номер", hint.Text);
        Assert.Equal(Visibility.Visible, hint.Visibility);
        field.Text = "123"; field.Select(1, 1); field.SelectedText = "9";
        Assert.Equal("193", field.Text); Assert.Equal(Visibility.Collapsed, hint.Visibility);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    });

    [Fact]
    public void SettingsAndHostedScreensExposeVisibleBackAndPreserveCurrentSection() => WpfTestHost.Run(() =>
    {
        static IEnumerable<TextBlock> Texts(DependencyObject root)
        {
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is TextBlock text) yield return text;
                foreach (var nested in Texts(child)) yield return nested;
            }
        }
        var menu = ShellTests.CreateMenu(new AppState());
        try
        {
            menu.Show(); menu.Navigate(MenuSection.Pokedex);
            var button = (Button)menu.FindName("SettingsButton");
            Assert.True(button.Focus());
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); menu.UpdateLayout();
            Assert.Contains(Texts(button), text => text.Text == "Назад" && text.IsVisible);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); menu.UpdateLayout();
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)menu.FindName("SettingsContent")).Visibility);
            Assert.Same(menu.FindName("CollectionTab"), ((TabControl)menu.FindName("MenuTabControl")).SelectedItem);
            Assert.Same(button, System.Windows.Input.Keyboard.FocusedElement);
            var hosted = menu.ShowHostedAsync(new TextBlock { Text = "Синтетическая история" }); menu.UpdateLayout();
            Assert.Contains(Texts(button), text => text.Text == "Назад" && text.IsVisible);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(hosted.IsCompleted);
            Assert.Same(menu.FindName("CollectionTab"), ((TabControl)menu.FindName("MenuTabControl")).SelectedItem);
        }
        finally { menu.Close(); }
    });

    [Fact]
    public void DuplicateIndividualsKeepTheSameLabelAcrossCollectionFiltersAndTeamSelection() => WpfTestHost.Run(() =>
    {
        var data = ApprovedExpeditionTests.State(3);
        var original = data.Individuals[0];
        data.Individuals = new[] { 3, 1, 2 }.Select(day => new PokemonIndividual {
            SpeciesId = original.SpeciesId, CurrentSpeciesId = original.CurrentSpeciesId, FamilyRootId = original.FamilyRootId,
            EvolutionChainId = original.EvolutionChainId, ChainLength = original.ChainLength, ReachedSpeciesIds = [original.CurrentSpeciesId],
            IsShiny = false, Nature = Nature.Bold, FirstTraitId = "gatherer", CaughtAtUtc = DateTimeOffset.Parse($"2026-01-0{day}T00:00:00Z")
        }).ToList();
        var state = new AppState(); state.Hydrate(data);
        var vm = new CollectionViewModel(state.Collection, state.CareStateByIndividualId, new CareModeService(), [], [], species: ApprovedExpeditionTests.Catalog.Value.Species);
        var collection = new CollectionView(vm, state, new SpriteCache(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())),
            () => { }, () => { }, () => { }, () => null, () => false, () => false);
        var cards = ((ItemsControl)collection.FindName("PokedexGrid")).Items.Cast<PokedexCard>().ToDictionary(p => p.IndividualId);
        Assert.Equal($"Пойман {data.Individuals[1].CaughtAtUtc.ToLocalTime():dd.MM.yyyy}", cards[data.Individuals[1].Id].IndividualText);
        Assert.Equal($"Пойман {data.Individuals[0].CaughtAtUtc.ToLocalTime():dd.MM.yyyy}", cards[data.Individuals[0].Id].IndividualText);
        Assert.All(cards.Values, card => Assert.Contains(card.IndividualText, card.AccessibleName));
        var expedition = ApprovedExpeditionTests.View(data); ApprovedExpeditionTests.Click(expedition, "ChooseTeamButton");
        foreach (var p in data.Individuals)
            Assert.Contains(cards[p.Id].IndividualText, System.Windows.Automation.AutomationProperties.GetName(ApprovedExpeditionTests.Candidate(expedition, p.Id)));
        ApprovedExpeditionTests.Candidate(expedition, data.Individuals[1].Id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ((ComboBox)expedition.FindName("PickerSortBox")).SelectedIndex = 2;
        ((TextBox)expedition.FindName("PickerSearchBox")).Text = data.Individuals[1].CurrentSpeciesId.ToString();
        Assert.Contains(cards[data.Individuals[1].Id].IndividualText, System.Windows.Automation.AutomationProperties.GetName(ApprovedExpeditionTests.Candidate(expedition, data.Individuals[1].Id)));
        ApprovedExpeditionTests.Click(expedition, "PickerDoneButton");
        var slot = (System.Windows.Controls.Primitives.UniformGrid)expedition.FindName("TeamSlots");
        Assert.Contains(cards[data.Individuals[1].Id].IndividualText, System.Windows.Automation.AutomationProperties.GetName(((Border)slot.Children[0]).Child));
        collection.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); expedition.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    });

    [Fact]
    public async Task ConfirmationRolePreservesCancelFocusAndResetsForSafeActions()
    {
        Task<bool> danger = null!, safe = null!;
        WpfTestHost.Run(() =>
        {
            var menu = ShellTests.CreateMenu(new AppState());
            try
            {
                menu.Show(); var overlay = (ConfirmationOverlay)menu.FindName("ConfirmationHost");
                danger = menu.ConfirmDestructiveAsync("Передать?", "Питомец покинет коллекцию.", "Передать");
                Assert.Same(overlay.FindResource("UiDangerButtonStyle"), ((Button)overlay.FindName("ConfirmButton")).Style);
                Assert.Same(overlay.FindName("CancelButton"), System.Windows.Input.Keyboard.FocusedElement);
                ((Button)overlay.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                safe = menu.ConfirmAsync("Купить?", "5 монет", "Купить");
                Assert.Same(overlay.FindResource("UiPrimaryButtonStyle"), ((Button)overlay.FindName("ConfirmButton")).Style);
                ((Button)overlay.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            finally { menu.Close(); }
        });
        Assert.False(await danger); Assert.False(await safe);
    }

    [Fact]
    public void ExpeditionEncounterExplanationDoesNotMentionOtherQueuedGifts() => WpfTestHost.Run(() =>
    {
        var state = new AppState();
        state.Encounters.Add(new EncounterState { Id = Guid.NewGuid(), Source = "gift", Phase = "sealed" });
        var encounter = new EncounterState { Id = Guid.NewGuid(), Source = "expedition", Phase = "resolved", KnownSpeciesId = 1,
            Individual = new PokemonIndividual { SpeciesId = 1, EvolutionChainId = 1, ChainLength = 3, Nature = Nature.Bold, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow } };
        state.Encounters.Add(encounter);
        var view = new CatchView(state, null, _ => Task.FromResult<byte[]?>(null), () => 0, () => { }, _ => "Бульбазавр");
        Assert.True(view.SelectEncounter(encounter.Id));
        var text = ((TextBlock)view.FindName("CatchCharmText")).Text;
        Assert.Contains("Результат встречи", text); Assert.DoesNotContain("Подароч", text); Assert.DoesNotContain("запасе", text);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    });

}
