using System.IO;
using System.Windows;
using System.Windows.Controls;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Tests.GlobalUpdate;

public sealed class ShopTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ShopTest_" + Guid.NewGuid().ToString("N"));
    private static string DataRoot => Path.Combine(AppContext.BaseDirectory, "Data");
    private static readonly Lazy<CatalogPackage> Catalog = new(() => CatalogPackage.Load(DataRoot));
    private readonly ShopService shop = new(DataRoot);

    public ShopTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);

    private static SaveData State(int coins = 100) => new()
    {
        SchemaVersion = 2, CatalogRevision = "test", BalanceRevision = "test", MigrationId = SaveMigration.MigrationId,
        Individuals = [], Settings = new(), ShopCoins = coins,
        Berries = SaveMigration.BerryIds.ToDictionary(id => id, _ => 0),
        Items = SaveMigration.ItemIds.ToDictionary(id => id, _ => 0),
        BerryRemainders = SaveMigration.BerryIds.ToDictionary(id => id, _ => new RationalRemainder())
    };

    [Fact]
    public async Task FailedCharmActivationShowsFailureAndPreservesLiveAndDiskInventory()
    {
        var data = State();
        data.Items["lucky-charm"] = 1;
        var path = Path.Combine(root, "activation-v2.json");
        var store = new JsonSaveStore(path);
        store.Save(data);
        var bytes = File.ReadAllBytes(path);
        Directory.CreateDirectory(path + ".tmp");
        var live = new AppState(); live.Hydrate(data);
        var writer = new BackgroundSaveWriter(store);
        Task? activation = null;
        ShopView? view = null;
        try
        {
            WpfTestHost.Run(() =>
            {
                view = new ShopView(live, shop, change => live.CommitAsync(writer, change, () => 0), () => { });
                activation = (Task)typeof(ShopView).GetMethod("ActivateLuckyCharm",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(view, [])!;
            });
            await activation!;
            WpfTestHost.Run(() =>
            {
                Assert.Equal(System.Windows.Visibility.Visible, ((System.Windows.FrameworkElement)view!.FindName("MessagePanel")).Visibility);
                var message = ((TextBlock)view.FindName("MessageText")).Text;
                Assert.False(string.IsNullOrWhiteSpace(message));
                Assert.DoesNotContain("активирован", message, StringComparison.OrdinalIgnoreCase);
                Assert.False(live.Shop.CatchLuckyCharmActive);
                Assert.Equal(1, live.Shop.Items["lucky-charm"]);
                Assert.Equal(100, live.Shop.Coins);
            });
            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.False(store.Load()!.ShopCatchLuckyCharmActive);
            Assert.Equal(1, store.Load()!.Items["lucky-charm"]);
        }
        finally { writer.Stop(); }
    }
    [Fact]
    public void AllTwelveGoodsUseBalanceAndSharedInventory()
    {
        Assert.Equal(12, shop.Goods.Count);
        Assert.Equal(6, shop.Goods.Count(g => g.IsBerry));
        var data = State(200);
        Assert.True(shop.TryPurchase(data, "cheri", 3, Guid.NewGuid()).Success);
        Assert.Equal(3, data.Berries["cheri"]);
        Assert.Equal(140, data.ShopCoins);
        Assert.True(shop.TryPurchase(data, "treats", 2, Guid.NewGuid()).Success);
        Assert.Equal(10, data.FeedingTreats);
        Assert.Equal(0, data.Items["treats"]);
        Assert.Equal(80, data.ShopCoins);
    }

    [Fact]
    public void ShortageOverflowAndInvalidQuantityNeverCharge()
    {
        var data = State(4);
        Assert.False(shop.TryPurchase(data, "oran", 1, Guid.NewGuid()).Success);
        Assert.False(shop.TryPurchase(data, "oran", 0, Guid.NewGuid()).Success);
        Assert.False(shop.TryPurchase(data, "oran", int.MaxValue, Guid.NewGuid()).Success);
        Assert.False(shop.TryPurchase(data, "missing", 1, Guid.NewGuid()).Success);
        Assert.Equal(4, data.ShopCoins);
        Assert.Equal(0, data.Berries["oran"]);
        Assert.Empty(data.CompletedOperations);
        var full = State(int.MaxValue);
        full.Berries["oran"] = int.MaxValue;
        Assert.False(shop.TryPurchase(full, "oran", 1, Guid.NewGuid()).Success);
        Assert.Equal(int.MaxValue, full.ShopCoins);
        Assert.Equal(int.MaxValue, full.Berries["oran"]);
    }

    [Fact]
    public void SameOperationIdReturnsSavedResultWithoutChargingTwice()
    {
        var data = State();
        var id = Guid.NewGuid();
        Assert.True(shop.TryPurchase(data, "oran", 2, id).Success);
        Assert.True(shop.TryPurchase(data, "oran", 2, id).Repeated);
        Assert.Equal(60, data.ShopCoins);
        Assert.Equal(2, data.Berries["oran"]);
    }

    [Theory]
    [InlineData("cheri", 2)]
    [InlineData("oran", 3)]
    public void PurchaseReplayWithDifferentInputsIsRejected(string itemId, int quantity)
    {
        var data = State();
        var id = Guid.NewGuid();
        Assert.True(shop.TryPurchase(data, "oran", 2, id).Success);

        var replay = shop.TryPurchase(data, itemId, quantity, id);

        Assert.False(replay.Success);
        Assert.False(replay.Repeated);
        Assert.Equal(60, data.ShopCoins);
        Assert.Equal(2, data.Berries["oran"]);
        Assert.Equal(0, data.Berries["cheri"]);
    }

    [Fact]
    public void LegacyPurchaseJournalEntryCannotClaimUnverifiableReplay()
    {
        var data = State();
        var id = Guid.NewGuid();
        data.CompletedOperations[$"shop:{id}"] = id;

        var replay = shop.TryPurchase(data, "oran", 1, id);

        Assert.False(replay.Success);
        Assert.Equal(100, data.ShopCoins);
        Assert.Equal(0, data.Berries["oran"]);
    }

    [Fact]
    public void PotionAndCareReplayMustTargetOriginalIndividual()
    {
        var levels = new LevelProgression(DataRoot);
        var data = State();
        var first = new PokemonIndividual { SpeciesId = 1, CurrentSpeciesId = 1, FamilyRootId = 1,
            EvolutionChainId = 1, ChainLength = 1, ReachedSpeciesIds = [1], Nature = Nature.Hardy,
            IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow, FirstTraitId = "gatherer", SecondTraitId = "tracker" };
        var second = new PokemonIndividual { SpeciesId = 1, CurrentSpeciesId = 1, FamilyRootId = 1,
            EvolutionChainId = 1, ChainLength = 1, ReachedSpeciesIds = [1], Nature = Nature.Hardy,
            IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow, FirstTraitId = "gatherer", SecondTraitId = "tracker" };
        data.Individuals.AddRange([first, second]);
        data.Items["xp-potion"] = 1;
        data.Items["care-booster"] = 1;
        var potionId = Guid.NewGuid();
        var careId = Guid.NewGuid();
        Assert.True(shop.TryUseXpPotion(data, first.Id, potionId, levels, Catalog.Value).Success);
        Assert.True(shop.TryUseCareBooster(data, first.Id, careId).Success);

        Assert.True(shop.TryUseXpPotion(data, first.Id, potionId, levels, Catalog.Value).Repeated);
        Assert.True(shop.TryUseCareBooster(data, first.Id, careId).Repeated);
        Assert.False(shop.TryUseXpPotion(data, second.Id, potionId, levels, Catalog.Value).Success);
        Assert.False(shop.TryUseCareBooster(data, second.Id, careId).Success);
        Assert.Equal(0, second.TotalXp);
        Assert.Equal(0, second.AccumulatedCarePoints);
        Assert.Equal(0, data.Items["xp-potion"]);
        Assert.Equal(0, data.Items["care-booster"]);
    }

    [Fact]
    public void ChoiceReplayWithDifferentItemOrChoiceCountIsRejected()
    {
        var data = State();
        data.Items["evolution-stone"] = 1;
        data.Items["offspring-charm"] = 1;
        var id = Guid.NewGuid();
        Assert.True(shop.TrySpendChoiceItem(data, "evolution-stone", 2, id).Success);

        Assert.True(shop.TrySpendChoiceItem(data, "evolution-stone", 2, id).Repeated);
        Assert.False(shop.TrySpendChoiceItem(data, "offspring-charm", 2, id).Success);
        Assert.False(shop.TrySpendChoiceItem(data, "evolution-stone", 3, id).Success);
        Assert.Equal(0, data.Items["evolution-stone"]);
        Assert.Equal(1, data.Items["offspring-charm"]);
    }

    [Theory]
    [InlineData("evolution-stone")]
    [InlineData("offspring-charm")]
    public void ChoiceItemIsSpentOnlyForMultipleOutcomes(string itemId)
    {
        var data = State();
        data.Items[itemId] = 1;
        Assert.False(shop.TrySpendChoiceItem(data, itemId, 1, Guid.NewGuid()).Success);
        Assert.Equal(1, data.Items[itemId]);
        var id = Guid.NewGuid();
        Assert.True(shop.TrySpendChoiceItem(data, itemId, 2, id).Success);
        Assert.True(shop.TrySpendChoiceItem(data, itemId, 2, id).Repeated);
        Assert.Equal(0, data.Items[itemId]);
    }

    [Fact]
    public async Task FailedCommitKeepsPublishedWalletAndStock()
    {
        var live = new AppState();
        live.Hydrate(State());
        var writer = new BackgroundSaveWriter(new JsonSaveStore(Path.Combine(root, "state-v2.json")),
            _ => throw new IOException("synthetic write failure"));
        await Assert.ThrowsAsync<IOException>(() => live.CommitAsync(writer, next =>
            shop.TryPurchase(next, "oran", 2, Guid.NewGuid()), () => 0));
        Assert.Equal(100, live.Shop.Coins);
        Assert.Equal(0, live.Shop.Berries["oran"]);
    }

    [Fact]
    public void PotionAtCapIsPreservedAndPartialGainReportsExcess()
    {
        var levels = new LevelProgression(DataRoot);
        var data = State();
        var pet = new PokemonIndividual
        {
            SpeciesId = 1, CurrentSpeciesId = 1, FamilyRootId = 1, EvolutionChainId = 1, ChainLength = 1,
            ReachedSpeciesIds = [1], Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow,
            FirstTraitId = "gatherer", SecondTraitId = "tracker", TotalXp = levels.MaxXp
        };
        data.Individuals.Add(pet);
        data.Items["xp-potion"] = 1;
        Assert.Equal((0, 300), shop.PotionPreview(pet, levels));
        Assert.False(shop.TryUseXpPotion(data, pet.Id, Guid.NewGuid(), levels, Catalog.Value).Success);
        Assert.Equal(1, data.Items["xp-potion"]);
        pet.TotalXp = levels.MaxXp - 80;
        Assert.Equal((80, 220), shop.PotionPreview(pet, levels));
        Assert.True(shop.TryUseXpPotion(data, pet.Id, Guid.NewGuid(), levels, Catalog.Value).Success);
        Assert.Equal(levels.MaxXp, pet.TotalXp);
        Assert.Equal(0, data.Items["xp-potion"]);
    }

    [Fact]
    public void CareBoosterAtIntegerCapDoesNotConsumeStock()
    {
        var data = State();
        var pet = new PokemonIndividual
        {
            SpeciesId = 1, EvolutionChainId = 1, ChainLength = 1, Nature = Nature.Hardy,
            IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow, AccumulatedCarePoints = int.MaxValue
        };
        data.Individuals.Add(pet);
        data.Items["care-booster"] = 1;
        Assert.False(shop.TryUseCareBooster(data, pet.Id, Guid.NewGuid()).Success);
        Assert.Equal(1, data.Items["care-booster"]);
    }

    [Fact]
    public void RecipePrefillsOnlyMissingQuantityAndCancelDoesNotSpend() => WpfTestHost.Run(() =>
    {
        var live = new AppState();
        live.Hydrate(State());
        live.Shop.Berries["cheri"] = 2;
        var commits = 0;
        var view = new ShopView(live, shop, _ => { commits++; return Task.CompletedTask; }, () => { });
        view.SelectBerryShortage("cheri", 5);
        Assert.Equal("3", Named<TextBox>(view, "QuantityBox").Text);
        Assert.StartsWith("Чери", Named<TextBlock>(view, "DetailNameText").Text);
        Assert.Equal(0, commits);
        Assert.Equal(100, live.Shop.Coins);
    });

    [Fact]
    public void ShortageVisitOffersOneWayBackThatDisappearsAfterUse() => WpfTestHost.Run(() =>
    {
        var live = new AppState();
        live.Hydrate(State());
        var view = new ShopView(live, shop, _ => Task.CompletedTask, () => { });
        var back = (Button)view.FindName("ReturnButton");
        Assert.Equal(Visibility.Collapsed, back.Visibility);
        var returned = 0;
        view.SetReturn("Вернуться в питомник", () => returned++);
        Assert.Equal(Visibility.Visible, back.Visibility);
        Assert.Equal("← Вернуться в питомник", back.Content);
        back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1, returned);
        Assert.Equal(Visibility.Collapsed, back.Visibility);
    });

    [Fact]
    public void TreatPriceIsLabeledPerPackAndBerryPricePerItem() => WpfTestHost.Run(() =>
    {
        var live = new AppState();
        live.Hydrate(State());
        var view = new ShopView(live, shop, _ => Task.CompletedTask, () => { });
        Pick(view, "treats");
        Assert.Equal("30 монет/пачка", Named<TextBlock>(view, "PriceText").Text);
        Pick(view, shop.Goods.First(g => g.IsBerry).Id);
        Assert.Equal("20 монет/шт.", Named<TextBlock>(view, "PriceText").Text);
    });

    [Fact]
    public void DoubleClickWhileCommitPendingStartsOnePurchase() => WpfTestHost.Run(() =>
    {
        var live = new AppState();
        live.Hydrate(State());
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var view = new ShopView(live, shop, _ => { calls++; return pending.Task; }, () => { }, confirmAsync: (_, _, _) => Task.FromResult(true));
        Pick(view, shop.Goods.First(g => g.IsBerry).Id);
        var buy = Named<Button>(view, "BuyButton");
        buy.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
        buy.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1, calls);
        pending.SetResult();
    });

    [Fact]
    public async Task RefusedPurchaseRetryRetainsOperationIdentity()
    {
        var live = new AppState(); live.Hydrate(State());
        var operations = new List<string>();
        var attempts = 0;
        ShopView? view = null;
        Task? buying = null;
        WpfTestHost.Run(() =>
        {
            view = new ShopView(live, shop, change =>
            {
                var next = live.ToSaveData(0);
                change(next);
                operations.Add(next.CompletedOperations.Keys.Single());
                if (++attempts == 1) throw new IOException("synthetic refusal");
                live.Hydrate(next);
                return Task.CompletedTask;
            }, () => { }, confirmAsync: (_, _, _) => Task.FromResult(true));
            buying = (Task)typeof(ShopView).GetMethod("Buy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(view, [shop.Goods.First(g => g.Id == "oran")])!;
        });
        await buying!;
        WpfTestHost.Run(() => buying = (Task)typeof(ShopView).GetMethod("Buy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(view, [shop.Goods.First(g => g.Id == "oran")])!);
        await buying!;
        Assert.Equal(2, operations.Count);
        Assert.Equal(operations[0], operations[1]);
        Assert.Equal(80, live.Shop.Coins);
        Assert.Equal(1, live.Shop.Berries["oran"]);
    }

    [Fact]
    public async Task PurchaseConfirmationCancelAndShortageNeverCommit()
    {
        var live = new AppState(); live.Hydrate(State(30));
        var commits = 0; var prompts = 0;
        ShopView? view = null; Task? buying = null;
        WpfTestHost.Run(() =>
        {
            view = new ShopView(live, shop, _ => { commits++; return Task.CompletedTask; }, () => { },
                confirmAsync: (_, body, _) => { prompts++; Assert.Contains("20 монет", body); return Task.FromResult(false); });
            buying = InvokeBuy(view, "oran");
        });
        await buying!;
        WpfTestHost.Run(() =>
        {
            view!.SelectBerryShortage("oran", 3);
            buying = InvokeBuy(view, "oran");
        });
        await buying!;
        Assert.Equal(1, prompts); Assert.Equal(0, commits);
        Assert.Equal(30, live.Shop.Coins); Assert.Equal(0, live.Shop.Berries["oran"]);
    }

    [Fact]
    public async Task LeavingWhilePurchaseConfirmationPendingDoesNotSpend()
    {
        var live = new AppState(); live.Hydrate(State());
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commits = 0; Task? buying = null;
        WpfTestHost.Run(() =>
        {
            var view = new ShopView(live, shop, _ => { commits++; return Task.CompletedTask; }, () => { },
                confirmAsync: (_, _, _) => pending.Task);
            buying = InvokeBuy(view, "oran");
            _ = InvokeBuy(view, "oran");
            view.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.UnloadedEvent));
        });
        pending.SetResult(true); await buying!;
        Assert.Equal(0, commits); Assert.Equal(100, live.Shop.Coins);
    }

    [Theory]
    [InlineData("care-booster")]
    [InlineData("xp-potion")]
    public void ApplyRoutesToPetSelectionWithoutTouchingActivePet(string item) => WpfTestHost.Run(() =>
    {
        var live = new AppState(); live.Hydrate(State());
        string? routed = null; var commits = 0;
        var view = new ShopView(live, shop, _ => { commits++; return Task.CompletedTask; }, () => { },
            openPetItems: id => routed = id);
        typeof(ShopView).GetMethod("OpenPetItems", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(view, [item]);
        Assert.Equal(item, routed); Assert.Equal(0, commits);
    });

    [Theory]
    [InlineData(1000, 760)]
    [InlineData(840, 650)]
    public void IllustratedShopHasAllGoodsAndReachableQuantityActions(double width, double height) => WpfTestHost.Run(() =>
    {
        var live = new AppState(); live.Hydrate(State(1000));
        var view = new ShopView(live, shop, _ => Task.CompletedTask, () => { });
        WpfTestHost.Layout(view, width - 40, height - 140);
        var shelf = Named<WrapPanel>(view, "GoodsPanel");
        Assert.Equal(12, shelf.Children.Count);
        var buy = Named<Button>(view, "BuyButton");
        foreach (Button tile in shelf.Children)
        {
            Assert.True(tile.ActualWidth >= 120 && tile.ActualHeight >= 100, $"{tile.Tag}: {tile.ActualWidth}x{tile.ActualHeight}");
            tile.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            view.UpdateLayout();
            Assert.NotNull(Named<System.Windows.Controls.Image>(view, "DetailArt").Source);
            Assert.True(Named<TextBox>(view, "QuantityBox").IsEnabled);
            Assert.True(buy.IsEnabled);
            // The buy button stays on the page whatever the good's description length.
            var bottom = buy.TranslatePoint(new System.Windows.Point(0, buy.ActualHeight), view).Y;
            Assert.True(buy.ActualHeight >= 40 && bottom <= view.ActualHeight + 1, $"{tile.Tag}: buy bottom {bottom} of {view.ActualHeight}");
        }
    });

    private static T Named<T>(ShopView view, string name) => (T)view.FindName(name);
    private static void Pick(ShopView view, string id) => Named<WrapPanel>(view, "GoodsPanel").Children.OfType<Button>()
        .Single(tile => (string)tile.Tag == id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public void ShelfFiltersByCategoryStepsByFiveAndKeepsEachGoodsQuantity() => WpfTestHost.Run(() =>
    {
        var live = new AppState(); live.Hydrate(State(1000));
        var view = new ShopView(live, shop, _ => Task.CompletedTask, () => { });
        var shelf = Named<WrapPanel>(view, "GoodsPanel"); var quantity = Named<TextBox>(view, "QuantityBox");
        var plus = Named<Button>(view, "IncreaseButton"); var minus = Named<Button>(view, "DecreaseButton");
        var berry = shop.Goods.First(g => g.IsBerry).Id;
        Pick(view, berry);
        Named<ListBox>(view, "StepBox").SelectedIndex = 1;
        plus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal("5", quantity.Text);
        plus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal("10", quantity.Text);
        minus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal("5", quantity.Text);
        minus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal("1", quantity.Text);
        quantity.Text = "7";
        Pick(view, "treats"); Assert.Equal("1", quantity.Text);
        Pick(view, berry); Assert.Equal("7", quantity.Text);

        Named<ListBox>(view, "CategoryBox").SelectedIndex = 3;
        var shown = shelf.Children.OfType<Button>().Where(tile => tile.Visibility == Visibility.Visible).Select(tile => (string)tile.Tag).ToArray();
        Assert.Equal(new[] { "lucky-charm", "offspring-charm" }, shown.Order().ToArray());
        Assert.Contains(shop.Goods.Single(g => g.Name == Named<TextBlock>(view, "DetailNameText").Text).Id, shown);
        // A shortage link from another screen always finds its good, whatever shelf was open.
        view.SelectBerryShortage(berry, 3);
        Assert.Equal(12, shelf.Children.OfType<Button>().Count(tile => tile.Visibility == Visibility.Visible));
        Assert.Equal("3", quantity.Text);
    });

    private Task InvokeBuy(ShopView view, string id) => (Task)typeof(ShopView).GetMethod("Buy",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(view, [shop.Goods.Single(g => g.Id == id)])!;

    [Fact]
    public void ShopApplyClosesOldDetailAndReturnsToOwnedRecipientCatalog() => WpfTestHost.Run(() =>
    {
        var live = new AppState(); live.Hydrate(State());
        var pet = new PokemonIndividual { SpeciesId = 1, CurrentSpeciesId = 1, FamilyRootId = 1,
            EvolutionChainId = 1, ChainLength = 1, Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow, Nickname = "Мой питомец" };
        live.Collection.Add(pet);
        var menu = ShellTests.CreateMenu(live);
        try
        {
            var collection = (CollectionView)((TabItem)menu.FindName("CollectionTab")).Content;
            ((TextBox)collection.FindName("PokedexSearchBox")).Text = "Мой";
            collection.OpenIndividual(pet.Id);
            Assert.Equal(System.Windows.Visibility.Visible, ((Border)collection.FindName("DetailOverlay")).Visibility);
            menu.Navigate(MenuSection.Shop);
            var view = new ShopView(live, shop, _ => throw new Exception("Route cannot mutate inventory"), () => { },
                openPetItems: _ =>
                {
                    menu.Navigate(MenuSection.Pokedex);
                    collection.OpenItemSelection();
                });
            typeof(ShopView).GetMethod("OpenPetItems", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(view, ["care-booster"]);
            Assert.Equal(System.Windows.Visibility.Collapsed, ((Border)collection.FindName("DetailOverlay")).Visibility);
            Assert.Equal(System.Windows.Visibility.Visible, ((StackPanel)collection.FindName("IndividualsPane")).Visibility);
            Assert.Equal(System.Windows.Visibility.Collapsed, ((StackPanel)collection.FindName("SpeciesPane")).Visibility);
            Assert.Equal("Мой", ((TextBox)collection.FindName("PokedexSearchBox")).Text);
            Assert.Null(collection.SelectedIndividualId);
        }
        finally { menu.Close(); }
    });

    [Fact]
    public async Task UiPurchaseRefusedDiskWriteKeepsStockAndReusesIdentityAfterWriterRepair()
    {
        var path = Path.Combine(root, "retry.json");
        var store = new JsonSaveStore(path); store.Save(State());
        var before = File.ReadAllBytes(path);
        var live = new AppState(); live.Hydrate(store.Load()!);
        var writer = new BackgroundSaveWriter(store);
        Directory.CreateDirectory(path + ".tmp");
        var operations = new List<string>();
        ShopView? view = null; Task? purchase = null;
        try
        {
            WpfTestHost.Run(() =>
            {
                view = new ShopView(live, shop, change => live.CommitAsync(writer, next =>
                {
                    change(next); operations.Add(next.CompletedOperations.Keys.Single());
                }, () => 0), () => { }, confirmAsync: (_, _, _) => Task.FromResult(true));
                purchase = InvokeBuy(view, "oran");
            });
            await purchase!;
            WpfTestHost.Run(() =>
            {
                var message = ((TextBlock)view!.FindName("MessageText")).Text;
                Assert.Contains("историю сохранений", message);
                Assert.DoesNotContain("Повторите покупку", message);
            });
            Assert.True(writer.HasFailed);
            Assert.Equal(before, File.ReadAllBytes(path)); Assert.Equal(100, live.Shop.Coins); Assert.Equal(0, live.Shop.Berries["oran"]);
            writer.Stop(); Directory.Delete(path + ".tmp");
            Assert.Equal(100, store.Load()!.ShopCoins);
            writer = new BackgroundSaveWriter(store);
            WpfTestHost.Run(() => purchase = InvokeBuy(view!, "oran"));
            await purchase!;
            Assert.Equal(2, operations.Count); Assert.Equal(operations[0], operations[1]);
            Assert.Equal(80, live.Shop.Coins); Assert.Equal(1, live.Shop.Berries["oran"]);
            Assert.Equal(80, store.Load()!.ShopCoins); Assert.Equal(1, store.Load()!.Berries["oran"]);
        }
        finally { writer.Stop(); }
    }

    [Fact]
    public void ShopHelpNeverDisplaysGameplayChanceFormulas() => WpfTestHost.Run(() =>
    {
        var live = new AppState(); live.Hydrate(State());
        var view = new ShopView(live, shop, _ => Task.CompletedTask, () => { });
        foreach (var good in shop.Goods.Where(g => !g.IsBerry))
        {
            Pick(view, good.Id);
            var effect = Named<TextBlock>(view, "DetailEffectText").Text;
            Assert.False(string.IsNullOrWhiteSpace(effect));
            Assert.DoesNotContain("шанс", effect, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("раза", effect, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("%", effect);
        }
    });

    [Fact]
    public async Task RefreshFailureAfterConfirmedPurchaseNeverReportsRefusedWrite()
    {
        var live = new AppState(); live.Hydrate(State());
        ShopView? view = null; Task? purchase = null;
        WpfTestHost.Run(() =>
        {
            view = new ShopView(live, shop, change =>
            {
                var next = live.ToSaveData(0); change(next); live.Hydrate(next); return Task.CompletedTask;
            }, () => throw new InvalidOperationException("synthetic repaint failure"),
                confirmAsync: (_, _, _) => Task.FromResult(true));
            purchase = InvokeBuy(view, "oran");
        });
        await purchase!;
        WpfTestHost.Run(() =>
        {
            var message = ((TextBlock)view!.FindName("MessageText")).Text;
            Assert.Contains("Покупка сохранена", message);
            Assert.DoesNotContain("Повторите покупку", message);
        });
        Assert.Equal(80, live.Shop.Coins); Assert.Equal(1, live.Shop.Berries["oran"]);
    }
    [Fact]
    public void NativeQuantityStepperSupportsEditingBoundsAndLocalShortage() => WpfTestHost.Run(() =>
    {
        var live = new AppState(); live.Hydrate(State());
        var commits = 0;
        var view = new ShopView(live, shop, _ => { commits++; return Task.CompletedTask; }, () => { });
        Pick(view, shop.Goods.First(g => g.IsBerry).Id);
        var minus = Named<Button>(view, "DecreaseButton"); var quantity = Named<TextBox>(view, "QuantityBox"); var plus = Named<Button>(view, "IncreaseButton");
        var buy = Named<Button>(view, "BuyButton"); var total = Named<TextBlock>(view, "TotalText");
        Assert.False(minus.IsEnabled);
        plus.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("2", quantity.Text); Assert.Contains("40 монет", total.Text);
        quantity.Select(0, 1); quantity.SelectedText = "3";
        Assert.Equal("3", quantity.Text); Assert.Contains("60 монет", total.Text);
        minus.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent)); Assert.Equal("2", quantity.Text);
        foreach (var invalid in new[] { "0", "-1", "abc", "2147483648", int.MaxValue.ToString() })
        {
            quantity.Text = invalid; Assert.False(buy.IsEnabled); Assert.False(string.IsNullOrWhiteSpace(total.Text));
            Assert.True(InvokeBuy(view, shop.Goods.First(g => g.IsBerry).Id).IsCompletedSuccessfully);
        }
        Assert.Equal(0, commits);
        quantity.Text = "6"; Assert.Contains("не хватает 20", total.Text); Assert.False(buy.IsEnabled);
        var berry = shop.Goods.First(g => g.IsBerry);
        live.Shop.Berries[berry.Id] = int.MaxValue - 1; view.Refresh();
        quantity.Text = "1"; Assert.False(plus.IsEnabled); Assert.True(buy.IsEnabled);
        live.Shop.Berries[berry.Id] = int.MaxValue; view.Refresh();
        Assert.False(plus.IsEnabled); Assert.False(buy.IsEnabled); Assert.Contains("нет места", total.Text);
        view.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.UnloadedEvent));
    });

}
