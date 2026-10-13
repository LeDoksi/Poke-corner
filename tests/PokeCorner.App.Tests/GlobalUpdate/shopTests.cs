using System.IO;
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
    public void AllTwelveGoodsUseBalanceAndSharedInventory()
    {
        Assert.Equal(12, shop.Goods.Count);
        Assert.Equal(6, shop.Goods.Count(g => g.IsBerry));
        var data = State();
        Assert.True(shop.TryPurchase(data, "cheri", 3, Guid.NewGuid()).Success);
        Assert.Equal(3, data.Berries["cheri"]);
        Assert.Equal(85, data.ShopCoins);
        Assert.True(shop.TryPurchase(data, "treats", 2, Guid.NewGuid()).Success);
        Assert.Equal(10, data.FeedingTreats);
        Assert.Equal(0, data.Items["treats"]);
        Assert.Equal(45, data.ShopCoins);
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
        Assert.Equal(90, data.ShopCoins);
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
        Assert.Equal(90, data.ShopCoins);
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
        var berries = (StackPanel)view.FindName("BerriesPanel");
        var cheri = (Border)berries.Children[0];
        var grid = (Grid)cheri.Child;
        var action = (StackPanel)grid.Children[2];
        Assert.Equal("3", ((TextBox)action.Children[0]).Text);
        Assert.Equal(0, commits);
        Assert.Equal(100, live.Shop.Coins);
    });

    [Fact]
    public void TreatPriceIsLabeledPerPackAndBerryPricePerItem() => WpfTestHost.Run(() =>
    {
        var live = new AppState();
        live.Hydrate(State());
        var view = new ShopView(live, shop, _ => Task.CompletedTask, () => { });
        var items = (StackPanel)view.FindName("ItemsPanel");
        var berries = (StackPanel)view.FindName("BerriesPanel");
        var treatsDetail = (StackPanel)((Grid)((Border)items.Children[0]).Child).Children[1];
        var berryDetail = (StackPanel)((Grid)((Border)berries.Children[0]).Child).Children[1];

        Assert.Contains("20 монет/пачка", ((TextBlock)treatsDetail.Children[2]).Text);
        Assert.Contains("5 монет/шт.", ((TextBlock)berryDetail.Children[2]).Text);
    });

    [Fact]
    public void DoubleClickWhileCommitPendingStartsOnePurchase() => WpfTestHost.Run(() =>
    {
        var live = new AppState();
        live.Hydrate(State());
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var view = new ShopView(live, shop, _ => { calls++; return pending.Task; }, () => { });
        var berries = (StackPanel)view.FindName("BerriesPanel");
        var grid = (Grid)((Border)berries.Children[0]).Child;
        var buy = (Button)((StackPanel)grid.Children[2]).Children[2];
        buy.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
        buy.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1, calls);
        pending.SetResult();
    });
}
