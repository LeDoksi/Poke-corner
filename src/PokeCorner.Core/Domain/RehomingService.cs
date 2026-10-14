using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Domain;

public sealed record RehomeQuote(int Coins, string? Reason);
public sealed record RehomeAction(bool Success, string Message, int Coins = 0, bool Repeated = false);

public sealed class RehomingService
{
    private readonly CatalogPackage catalog;
    private readonly SaveData state;
    private readonly int[] rarityCoins;
    private readonly int levelStep;
    private readonly int coinsPerStep;
    private readonly int shinyBonus;
    private readonly LevelProgression levels;

    public RehomingService(CatalogPackage catalog, SaveData state)
    {
        this.catalog = catalog;
        this.state = state;
        using var balance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "balance.json")));
        var rehome = balance.RootElement.GetProperty("rehome");
        rarityCoins = rehome.GetProperty("coinsByRarity").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        levelStep = rehome.GetProperty("levelBonusEvery").GetInt32();
        coinsPerStep = rehome.GetProperty("coinsPerLevelStep").GetInt32();
        shinyBonus = rehome.GetProperty("shinyBonus").GetInt32();
        if (rarityCoins.Length != 4 || rarityCoins.Any(x => x < 0) || levelStep <= 0 || coinsPerStep < 0 || shinyBonus < 0)
            throw new InvalidDataException("Неверный баланс передачи питомцев.");
        levels = new LevelProgression(catalog.DataRoot);
    }

    public RehomeQuote Quote(Guid individualId, Guid? replacementId = null)
    {
        if (state.SchemaVersion != 2) return new(0, "Сначала требуется сохранение v2.");
        var individual = state.Individuals.FirstOrDefault(x => x.Id == individualId);
        if (individual is null) return new(0, "Этот покемон больше не найден.");
        if (state.Individuals.Count <= 1) return new(0, "Последнего покемона передать нельзя.");
        if (individual.IsFavorite) return new(0, "Сначала снимите защиту избранного покемона.");
        var busy = ExpeditionService.BusyIndividualIds(state);
        if (busy.Contains(individualId)) return new(0, "Покемон занят в походе или питомнике.");
        if (state.ActiveIndividualId == individualId &&
            (replacementId is null || replacementId == individualId ||
             !state.Individuals.Any(x => x.Id == replacementId) || busy.Contains(replacementId.Value)))
            return new(0, "Сначала выберите доступную замену активному покемону.");
        if (state.ActiveIndividualId != individualId && replacementId is not null)
            return new(0, "Замена нужна только активному покемону.");
        var species = catalog.Species.FirstOrDefault(x => x.Id == individual.CurrentSpeciesId);
        if (species is null || !Enum.TryParse<RarityTier>(species.Rarity, true, out var rarity) || !Enum.IsDefined(rarity))
            return new(0, "Редкость этого вида не найдена в каталоге.");
        var coins = checked(rarityCoins[(int)rarity] + levels.LevelForXp(individual.TotalXp) / levelStep * coinsPerStep
            + (individual.IsShiny ? shinyBonus : 0));
        if (state.ShopCoins > int.MaxValue - coins) return new(0, "Кошелёк не может вместить эту сумму.");
        return new(coins, null);
    }

    public RehomeAction Transfer(Guid individualId, Guid operationId, Guid? replacementId = null)
    {
        if (state.SchemaVersion != 2 || operationId == Guid.Empty)
            return new(false, "Требуется сохранение v2 и ID действия.");
        var key = $"rehome:{operationId}";
        var fingerprint = Fingerprint(individualId, replacementId);
        if (state.CompletedOperations.TryGetValue(key, out var previous))
            return previous == fingerprint && state.RehomingResults.TryGetValue(operationId, out var result)
                && result.IndividualId == individualId && result.ReplacementId == replacementId
                ? new(true, "Передача уже сохранена.", result.Coins, true)
                : new(false, "ID действия уже использован с другим покемоном или заменой.");
        var quote = Quote(individualId, replacementId);
        if (quote.Reason is not null) return new(false, quote.Reason);
        var individual = state.Individuals.Single(x => x.Id == individualId);
        state.DiscoveredSpeciesIds.UnionWith(individual.ReachedSpeciesIds);
        state.DiscoveredSpeciesIds.Add(individual.SpeciesId);
        if (individual.IsShiny) state.ShinySpeciesIds.UnionWith(individual.ReachedSpeciesIds);
        state.Individuals.Remove(individual);
        state.CareStates.Remove(individualId);
        if (state.ActiveIndividualId == individualId) state.ActiveIndividualId = replacementId;
        state.ShopCoins += quote.Coins;
        state.CompletedOperations[key] = fingerprint;
        state.RehomingResults[operationId] = new(individualId, replacementId, quote.Coins);
        return new(true, "Покемон передан в новый дом.", quote.Coins);
    }

    internal static Guid Fingerprint(Guid individualId, Guid? replacementId) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"rehome:{individualId:N}:{replacementId?.ToString("N") ?? "none"}")).AsSpan(0, 16));
}
