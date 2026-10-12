using System.Text.Json;
using PokeCorner.Core.Persistence;

namespace PokeCorner.Core.Domain;

public sealed record ShopGood(string Id, string Name, string Effect, int Price, int Units, bool IsBerry);
public sealed record PurchaseResult(bool Success, string Message, bool Repeated = false);

public sealed class ShopService
{
    private readonly string? _dataRoot;
    private IReadOnlyList<ShopGood>? _goods;

    public ShopService(string? dataRoot = null) => _dataRoot = dataRoot;
    public string DataRoot => _dataRoot ?? Path.Combine(AppContext.BaseDirectory, "Data");

    public IReadOnlyList<ShopGood> Goods => _goods ??= LoadGoods();

    private IReadOnlyList<ShopGood> LoadGoods()
    {
        var root = DataRoot;
        using var balance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "final", "balance.json")));
        var prices = balance.RootElement.GetProperty("shopPrices");
        var pack = balance.RootElement.GetProperty("treatPackQuantity").GetInt32();
        var care = balance.RootElement.GetProperty("careBoosterPoints").GetInt32();
        var xp = balance.RootElement.GetProperty("xpPotionXp").GetInt32();
        var goods = new List<ShopGood>
        {
            new("treats", "Лакомства", $"Пачка: {pack} шт.", prices.GetProperty("treats5").GetInt32(), pack, false),
            new("care-booster", "Бустер ухода", $"+{care:N0} CP выбранному питомцу", prices.GetProperty("careBooster7500").GetInt32(), care, false),
            new("lucky-charm", "Shiny-талисман", "×4 к следующему допустимому shiny-шансу", prices.GetProperty("luckyCharm").GetInt32(), 1, false),
            new("evolution-stone", "Камень выбора", "Выбор доступной ветви эволюции", prices.GetProperty("evolutionStone").GetInt32(), 1, false),
            new("offspring-charm", "Талисман потомства", "Выбор доступного вида в гнезде", prices.GetProperty("offspringCharm").GetInt32(), 1, false),
            new("xp-potion", "Зелье опыта", $"+{xp} XP выбранному питомцу", prices.GetProperty("xpPotion300").GetInt32(), xp, false)
        };
        foreach (var (id, name) in new[] { ("cheri", "Чери"), ("oran", "Оран"), ("aspear", "Аспир"), ("pecha", "Печа"), ("chesto", "Често"), ("rawst", "Роуст") })
            goods.Add(new(id, name, "Ягода для рецептов", prices.GetProperty("berry1").GetInt32(), 1, true));
        if (goods.Count != 12 || goods.Any(g => g.Price <= 0 || g.Units <= 0) || goods.Select(g => g.Id).Distinct().Count() != 12)
            throw new InvalidDataException("Invalid shop balance.");
        return goods;
    }

    public PurchaseResult TryPurchase(SaveData next, string itemId, int quantity, Guid operationId)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (operationId == Guid.Empty) return new(false, "Неверный идентификатор покупки.");
        if (next.SchemaVersion != 2) return new(false, "Требуется обновлённое сохранение.");
        if (next.CompletedOperations.ContainsKey($"shop:{operationId}")) return new(true, "Покупка уже сохранена.", true);
        var good = Goods.FirstOrDefault(g => g.Id == itemId);
        if (good is null) return new(false, "Товар не найден.");
        if (quantity <= 0) return new(false, "Укажите положительное количество.");
        var stock = good.Id == "treats" ? next.FeedingTreats : (good.IsBerry ? next.Berries : next.Items)[good.Id];
        var added = (long)good.Units * quantity;
        var cost = (long)good.Price * quantity;
        if (added > int.MaxValue - stock || cost > int.MaxValue) return new(false, "Количество слишком велико.");
        if (next.ShopCoins < cost) return new(false, $"Не хватает {cost - next.ShopCoins} монет.");
        next.ShopCoins -= (int)cost;
        if (good.Id == "treats") next.FeedingTreats += (int)added;
        else (good.IsBerry ? next.Berries : next.Items)[good.Id] += (int)added;
        next.CompletedOperations[$"shop:{operationId}"] = operationId;
        return new(true, $"Куплено: {good.Name} ×{quantity}.");
    }

    public (int Useful, int Excess) PotionPreview(PokemonIndividual individual, LevelProgression levels)
    {
        var amount = Goods.Single(g => g.Id == "xp-potion").Units;
        var useful = Math.Min(amount, Math.Max(0, levels.MaxXp - individual.TotalXp));
        return (useful, amount - useful);
    }

    public PurchaseResult TryUseXpPotion(SaveData next, Guid individualId, Guid operationId, LevelProgression levels)
    {
        if (operationId == Guid.Empty) return new(false, "Неверный идентификатор действия.");
        if (next.CompletedOperations.ContainsKey($"potion:{operationId}")) return new(true, "Зелье уже применено.", true);
        var individual = next.Individuals.FirstOrDefault(i => i.Id == individualId);
        if (individual is null) return new(false, "Питомец не найден.");
        if (next.Items["xp-potion"] <= 0) return new(false, "Нет зелья опыта.");
        var (useful, excess) = PotionPreview(individual, levels);
        if (useful == 0) return new(false, "Максимальный уровень: зелье не расходуется.");
        levels.AddXp(individual, useful);
        next.Items["xp-potion"]--;
        next.CompletedOperations[$"potion:{operationId}"] = operationId;
        return new(true, $"Получено {useful} XP; излишек {excess} XP.");
    }

    public PurchaseResult TryUseCareBooster(SaveData next, Guid individualId, Guid operationId)
    {
        if (operationId == Guid.Empty) return new(false, "Неверный идентификатор действия.");
        if (next.CompletedOperations.ContainsKey($"care-booster:{operationId}")) return new(true, "Бустер уже применён.", true);
        var individual = next.Individuals.FirstOrDefault(i => i.Id == individualId);
        if (individual is null) return new(false, "Питомец не найден.");
        if (next.Items["care-booster"] <= 0) return new(false, "Нет бустера ухода.");
        var gained = Math.Min(Goods.Single(g => g.Id == "care-booster").Units, int.MaxValue - individual.AccumulatedCarePoints);
        if (gained == 0) return new(false, "Прогресс ухода уже максимален.");
        individual.AccumulatedCarePoints += gained;
        next.Items["care-booster"]--;
        next.CompletedOperations[$"care-booster:{operationId}"] = operationId;
        return new(true, $"Добавлено {gained} CP ухода.");
    }

    // Call inside the evolution/nest snapshot so the chosen outcome and resource spend commit together.
    public PurchaseResult TrySpendChoiceItem(SaveData next, string itemId, int availableChoices, Guid operationId)
    {
        if (itemId is not ("evolution-stone" or "offspring-charm")) return new(false, "Неверный предмет выбора.");
        if (operationId == Guid.Empty) return new(false, "Неверный идентификатор действия.");
        if (next.CompletedOperations.ContainsKey($"choice:{operationId}")) return new(true, "Выбор уже сохранён.", true);
        if (availableChoices <= 1) return new(false, "Доступен только один исход: предмет не расходуется.");
        if (next.Items[itemId] <= 0) return new(false, "Предмета нет в инвентаре.");
        next.Items[itemId]--;
        next.CompletedOperations[$"choice:{operationId}"] = operationId;
        return new(true, "Предмет выбора применён.");
    }
    // ~10 coins/hour of active play at the recalibrated 9000-units/hour baseline (see
    // FeedingService.ActivityUnitsPerTreatEarned's comment) — one coin roughly every 6
    // minutes of engaged play.
    public const long ActivityUnitsPerCoinEarned = 900;

    public const double CatchLuckyCharmShinyMultiplier = 4.0;

    public void EarnCoinsFromActivity(ShopInventory shop, long activityUnits)
    {
        var totalUnits = shop.RemainderUnits + activityUnits;
        shop.Coins += (int)(totalUnits / ActivityUnitsPerCoinEarned);
        shop.RemainderUnits = totalUnits % ActivityUnitsPerCoinEarned;
    }

    // Read-and-clear in one place so a catch and an egg hatch spend the charm identically:
    // whichever happens first gets the boost, regardless of whether the roll comes up shiny.
    public double ConsumeCatchLuckyCharm(ShopInventory shop)
    {
        if (!shop.CatchLuckyCharmActive) return 1.0;
        shop.CatchLuckyCharmActive = false;
        return CatchLuckyCharmShinyMultiplier;
    }

}
