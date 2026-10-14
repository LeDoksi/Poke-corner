using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Domain;

public sealed record RetrainingCost(string BerryId, int Required, int Owned);
public sealed record RetrainingPreview(IReadOnlyList<RetrainingCost> Costs, IReadOnlyList<string> Outcomes,
    string? CurrentTraitId, string? Reason);
public sealed record RetrainingAction(bool Success, string Message, string? TraitId = null, bool Repeated = false);

public sealed class RetrainingService
{
    private readonly CatalogPackage catalog;
    private readonly SaveData state;
    private readonly TraitRules traits;
    private readonly Dictionary<int, Dictionary<string, int>> recipes;

    public RetrainingService(CatalogPackage catalog, SaveData state)
    {
        this.catalog = catalog;
        this.state = state;
        traits = new TraitRules(catalog.DataRoot);
        using var rules = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "catalog-rules.json")));
        recipes = rules.RootElement.GetProperty("retraining").EnumerateArray().ToDictionary(
            row => row.GetProperty("speciesId").GetInt32(),
            row => row.GetProperty("berries").EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetInt32()));
        if (recipes.Values.Any(recipe => recipe.Count != 3 || recipe.Values.Any(count => count != 20)
            || recipe.Keys.Any(id => !SaveMigration.BerryIds.Contains(id))))
            throw new InvalidDataException("Неверный рецепт переобучения в каталоге.");
    }

    public RetrainingPreview Preview(Guid individualId, int slot)
    {
        if (state.SchemaVersion != 2) return new([], [], null, "Сначала требуется сохранение v2.");
        var individual = state.Individuals.FirstOrDefault(x => x.Id == individualId);
        if (individual is null) return new([], [], null, "Этот покемон больше не найден.");
        if (slot is not (1 or 2)) return new([], [], null, "Выберите открытый слот особенности.");
        var current = slot == 1 ? individual.FirstTraitId : individual.SecondTraitId;
        var other = slot == 1 ? individual.SecondTraitId : individual.FirstTraitId;
        var costs = (recipes.GetValueOrDefault(individual.CurrentSpeciesId) ?? [])
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new RetrainingCost(x.Key, x.Value, state.Berries.GetValueOrDefault(x.Key))).ToArray();
        var outcomes = traits.Allowed(individual.CurrentSpeciesId, individual.IsShiny, catalog)
            .Where(id => id != current && id != other).ToArray();
        var reason = string.IsNullOrWhiteSpace(current) ? "Этот слот ещё не открыт."
            : costs.Length != 3 ? "Для этого вида нет рецепта переобучения."
            : ExpeditionService.BusyIndividualIds(state).Contains(individualId) ? "Покемон занят в походе или питомнике."
            : outcomes.Length == 0 ? "Нет доступных новых особенностей."
            : costs.FirstOrDefault(x => x.Owned < x.Required) is { } missing
                ? $"Не хватает {missing.Required - missing.Owned} ягод {missing.BerryId}." : null;
        return new(costs, outcomes, current, reason);
    }

    public RetrainingAction Retrain(Guid individualId, int slot, Guid operationId)
    {
        if (operationId == Guid.Empty) return new(false, "Требуется ID действия.");
        var key = $"retrain:{operationId}";
        var fingerprint = Fingerprint(individualId, slot);
        if (state.CompletedOperations.TryGetValue(key, out var previous))
            return previous == fingerprint && state.RetrainingResults.TryGetValue(operationId, out var saved)
                ? new(true, "Переобучение уже сохранено.", saved.TraitId, true)
                : new(false, "ID действия уже использован с другим покемоном или слотом.");
        var preview = Preview(individualId, slot);
        if (preview.Reason is not null) return new(false, preview.Reason);
        var individual = state.Individuals.Single(x => x.Id == individualId);
        var chosen = preview.Outcomes[DrawIndex(operationId, individualId, slot, preview.Outcomes.Count)];
        foreach (var cost in preview.Costs) state.Berries[cost.BerryId] -= cost.Required;
        if (slot == 1) individual.FirstTraitId = chosen;
        else individual.SecondTraitId = chosen;
        AchievementMetrics.Record(state, "special:trophy-special-retrain");
        state.CompletedOperations[key] = fingerprint;
        state.RetrainingResults[operationId] = new(individualId, slot, chosen);
        return new(true, $"Новая особенность: {chosen}.", chosen);
    }

    public static Guid Fingerprint(Guid individualId, int slot) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"retrain:{individualId:N}:{slot}"))[..16]);

    private static int DrawIndex(Guid operationId, Guid individualId, int slot, int count)
    {
        var range = 1UL << 32;
        var limit = range - range % (uint)count;
        for (var counter = 0; ; counter++)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
                $"retrain-draw:{operationId:N}:{individualId:N}:{slot}:{counter}"));
            var sample = BinaryPrimitives.ReadUInt32LittleEndian(hash);
            if (sample < limit) return (int)(sample % (uint)count);
        }
    }
}
