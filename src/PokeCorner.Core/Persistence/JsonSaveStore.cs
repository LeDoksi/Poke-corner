using System.Text.Json;
using System.Text;
using PokeCorner.Core.Activity;
using PokeCorner.Core.Domain;

namespace PokeCorner.Core.Persistence;

public sealed class JsonSaveStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public JsonSaveStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    public static string Serialize(SaveData data) => JsonSerializer.Serialize(data, Options);

    public static SaveData Clone(SaveData data) => JsonSerializer.Deserialize<SaveData>(Serialize(data), Options)!;

    public void Save(SaveData data) => SaveSerialized(Serialize(data));

    public void SaveSerialized(string json)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write to a temp file first, then atomically move it into place: a crash mid-write
        // leaves either the old file intact or the new file complete, never a truncated one.
        var tempPath = _filePath + ".tmp";
        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true))
            {
                writer.Write(json);
                writer.Flush();
            }
            stream.Flush(flushToDisk: true);
        }
        File.Move(tempPath, _filePath, overwrite: true);
    }

    public void Delete()
    {
        if (File.Exists(_filePath)) File.Delete(_filePath);
    }

    public SaveData? Load() => Load(out _);

    public SaveData? Load(out byte[]? originalBytes)
    {
        originalBytes = null;
        try
        {
            originalBytes = File.ReadAllBytes(_filePath);
            using var reader = new StreamReader(new MemoryStream(originalBytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var json = reader.ReadToEnd();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Require(root.ValueKind == JsonValueKind.Object, "корень JSON должен быть объектом");
            if (root.TryGetProperty("SchemaVersion", out var version))
                Require(version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var number) && number is 1 or 2,
                    "неподдерживаемая версия схемы");

            // Id has a NewGuid initializer for NEW individuals/eggs; a missing saved identity
            // must be rejected before deserialization can silently replace it.
            var identities = new HashSet<Guid>();
            ValidateIdentities(root, "Individuals", identities, required: true);
            ValidateIdentities(root, "PendingEggs", identities, required: false);
            if (root.TryGetProperty("Gift", out var gift) && gift.ValueKind == JsonValueKind.Object &&
                gift.TryGetProperty("Gifts", out var gifts) && gifts.ValueKind == JsonValueKind.Array)
            {
                var giftIds = new HashSet<Guid>();
                foreach (var entry in gifts.EnumerateArray())
                    Require(entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("Individual", out var individual)
                        && individual.ValueKind == JsonValueKind.Object && individual.TryGetProperty("Id", out var id)
                        && id.ValueKind == JsonValueKind.String && id.TryGetGuid(out var guid) && guid != Guid.Empty && giftIds.Add(guid), "неверный GUID подарка");
            }
            if (root.TryGetProperty("SchemaVersion", out var schema) && schema.GetInt32() == 2)
                foreach (var field in new[] { "CatalogRevision", "BalanceRevision", "MigrationId", "DiscoveredSpeciesIds", "EvolutionTransitions",
                    "ShinySpeciesIds", "Metrics", "AwardLedger", "Berries", "Items", "BerryRemainders", "Tutorial", "Gift", "CompletedOperations" })
                    Require(root.TryGetProperty(field, out var property) && property.ValueKind != JsonValueKind.Null, $"отсутствует {field} v2");
            var data = JsonSerializer.Deserialize<SaveData>(json, Options);
            Require(data is not null, "пустое сохранение");
            ValidateData(data!);
            return data;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new SaveLoadException(_filePath, ex.Message, ex);
        }
    }

    private static void Require(bool condition, string diagnostic)
    {
        if (!condition) throw new JsonException(diagnostic);
    }

    private static void ValidateIdentities(JsonElement root, string name, HashSet<Guid> identities, bool required)
    {
        if (!root.TryGetProperty(name, out var list))
        {
            Require(!required, $"отсутствует {name}");
            return;
        }
        Require(list.ValueKind == JsonValueKind.Array, $"{name} должен быть массивом");
        foreach (var item in list.EnumerateArray())
        {
            Require(item.ValueKind == JsonValueKind.Object, $"пустой элемент {name}");
            Require(item.TryGetProperty("Id", out var id) && id.ValueKind == JsonValueKind.String
                && id.TryGetGuid(out var guid) && guid != Guid.Empty && identities.Add(guid),
                $"отсутствующий, неверный или повторный GUID в {name}");
        }
    }

    public static void ValidateData(SaveData data)
    {
        Require(data.Individuals is not null && data.Settings is not null
            && data.CareStates is not null && data.PendingEggs is not null, "обязательное поле равно null");
        Require(data.FeedingTreats >= 0 && data.CatchAccumulatorUnits >= 0 && data.FeedingRemainderUnits >= 0
            && data.ShopCoins >= 0 && data.ShopRemainderUnits >= 0 && data.PendingCatchCount >= 0,
            "отрицательный счётчик");
        Require(data.CatchAccumulatorUnits < ActivityAccumulator.CatchCheckIntervalUnits
            && data.FeedingRemainderUnits < FeedingService.ActivityUnitsPerTreatEarned
            && data.ShopRemainderUnits < ShopService.ActivityUnitsPerCoinEarned, "остаток вне интервала начисления");
        Require(data.SchemaVersion is 1 or 2, "неподдерживаемая версия схемы");
        foreach (var individual in data.Individuals!)
        {
            Require(individual is not null && individual.Id != Guid.Empty, "неверный экземпляр");
            Require(individual!.SpeciesId > 0 && individual.EvolutionChainId > 0 && individual.ChainLength is >= 1 and <= 3,
                "неверный вид или эволюционная цепочка");
            Require(Enum.IsDefined(individual.Nature) && Enum.IsDefined(individual.CurrentStage)
                && (int)individual.CurrentStage < individual.ChainLength && individual.AccumulatedCarePoints >= 0,
                "неверная стадия или прогресс");
            Require(individual.DisplayStageOverride is null || (individual.DisplayStageOverride >= 0
                && individual.DisplayStageOverride <= (int)individual.CurrentStage), "неверная отображаемая стадия");
        }
        Require(data.Individuals!.Select(i => i.Id).Distinct().Count() == data.Individuals.Count, "повторный GUID");
        if (data.SchemaVersion == 2) ValidateV2(data);
        Require(data.ActiveIndividualId is null || data.Individuals!.Any(i => i.Id == data.ActiveIndividualId),
            "активный GUID отсутствует в коллекции");
        Require(data.CareStates!.All(pair => pair.Key != Guid.Empty && pair.Value is not null), "неверное состояние ухода");
        Require(data.PendingEggs!.All(egg => egg is not null && Enum.IsDefined(egg.Tier)), "неверная редкость яйца");
        Require(Enum.IsDefined(data.Settings!.CareMode) && data.Settings.MonitorIndex >= 0
            && data.Settings.PetSizePixels > 0 && data.Settings.CrySoundVolume is >= 0 and <= 100 && data.Settings.EffectsSoundVolume is >= 0 and <= 100
            && (data.Settings.PetPositionX is null) == (data.Settings.PetPositionY is null),
            "неверные настройки");
    }
    private static void ValidateV2(SaveData data)
    {
        Require(!string.IsNullOrWhiteSpace(data.CatalogRevision) && !string.IsNullOrWhiteSpace(data.BalanceRevision)
            && data.MigrationId == SaveMigration.MigrationId, "неверные ревизии v2");
        Require(data.DiscoveredSpeciesIds is not null && data.EvolutionTransitions is not null && data.ShinySpeciesIds is not null
            && data.Metrics is not null && data.AwardLedger is not null && data.Berries is not null && data.Items is not null
            && data.BerryRemainders is not null && data.Tutorial is not null && data.Gift is not null
            && data.CompletedOperations is not null, "обязательное поле v2 равно null");
        Require(data.Individuals.All(i => i.CurrentSpeciesId > 0 && i.FamilyRootId > 0 && i.TotalXp is >= 0 and <= 6400
            && SaveMigration.TraitIds.Contains(i.FirstTraitId)
            && (i.SecondTraitId is null || SaveMigration.TraitIds.Contains(i.SecondTraitId) && i.SecondTraitId != i.FirstTraitId)
            && i.ReachedSpeciesIds is { Count: > 0 } && i.ParentIds is not null
            && i.ReachedSpeciesIds[^1] == i.CurrentSpeciesId
            && (i.DisplayStageOverride is null || i.DisplayStageOverride < i.ReachedSpeciesIds.Count)
            && i.ReachedSpeciesIds.All(id => id > 0) && i.ReachedSpeciesIds.Distinct().Count() == i.ReachedSpeciesIds.Count), "неверный экземпляр v2");
        Require(SaveMigration.BerryIds.ToHashSet().SetEquals(data.Berries!.Keys) && SaveMigration.ItemIds.ToHashSet().SetEquals(data.Items!.Keys)
            && data.Berries.Values.All(n => n >= 0) && data.Items.Values.All(n => n >= 0) && data.Items["treats"] == data.FeedingTreats, "неверный инвентарь v2");
        Require(SaveMigration.BerryIds.ToHashSet().SetEquals(data.BerryRemainders!.Keys)
            && data.BerryRemainders.Values.All(r => r is not null && r.Denominator > 0 && r.Numerator >= 0 && r.Numerator < r.Denominator), "неверный дробный остаток");
        Require(data.DiscoveredSpeciesIds!.All(id => id > 0) && data.ShinySpeciesIds!.All(id => id > 0)
            && data.Metrics!.Values.All(n => n is null or >= 0), "неверная метрика v2");
        Require(data.AwardLedger!.Values.All(a => a is not null && a.CoinsGranted >= 0 && a.ClaimStatus is "claimed" or "unclaimed"
            && a.AvailableSpeciesIds is not null), "неверный журнал наград");
        Require(data.Tutorial!.TutorialRevision == SaveMigration.MigrationId && data.Tutorial.CurrentStep is >= 1 and <= 6
            && data.Tutorial.Status is "not-started" or "in-progress" or "completed" or "skipped" && data.Tutorial.SeenHintKeys is not null, "неверный тур");
        Require(data.Gift!.GrantId == "global-update-compatible-pair-v1" && data.Gift.Gifts is not null
            && data.Gift.Gifts.Count is 0 or 2, "неверный подарок");
        var ids = data.Individuals.Select(i => i.Id).ToHashSet();
        foreach (var gift in data.Gift.Gifts!)
        {
            Require(gift is not null && gift.Status is "reserved" or "opened" && gift.Individual is not null
                && gift.Individual.Id != Guid.Empty && gift.Individual.CurrentSpeciesId > 0 && SaveMigration.TraitIds.Contains(gift.Individual.FirstTraitId), "неверная выдача");
            Require(gift!.Status != "reserved" || ids.Add(gift.Individual!.Id), "повторный GUID подарка");
        }
        var ownedIds = data.Individuals.Select(i => i.Id).ToHashSet();
        ValidateActivity(data.Expedition, ownedIds, "claimed");
        ValidateActivity(data.Nest, ownedIds, "hatched");
        Require(data.Expedition is null || data.Nest is null || data.Expedition.Id != data.Nest.Id, "повторный ID таймера");
        if (data.Expedition?.Status is "running" or "ready" && data.Nest?.Status is "running" or "ready")
            Require(!data.Expedition.ParticipantIds.Intersect(data.Nest.ParticipantIds).Any(), "участник занят дважды");
    }

    private static void ValidateActivity(TimedActivity? activity, HashSet<Guid> ids, string terminal)
    {
        if (activity is null) return;
        Require(activity.Id != Guid.Empty && activity.ParticipantIds is { Count: > 0 }
            && activity.ParticipantIds.Distinct().Count() == activity.ParticipantIds.Count
            && activity.DurationMinutes > 0 && activity.ReadyAtUtc >= activity.StartedAtUtc
            && (activity.Status is "running" or "ready" || activity.Status == terminal)
            && activity.ResourcePrices is not null && activity.ResourcePrices.Values.All(n => n >= 0), "неверный таймер v2");
        Require(activity.Status == terminal || activity.ParticipantIds.All(ids.Contains), "участник таймера отсутствует");
    }

}
