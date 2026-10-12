using System.Text.Json;
using System.Text;
using PokeCorner.Core.Activity;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Persistence;

public sealed class JsonSaveStore
{
    private readonly string _filePath;
    private byte[]? _baseline;
    private string? _firstWriteLegacyPath;
    private byte[]? _firstWriteLegacyBytes;
    private bool _resolved;
    private bool _blocked;
    public SaveHistory History { get; }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public JsonSaveStore(string filePath, Func<DateTimeOffset>? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
        History = new SaveHistory(_filePath, clock);
    }

    public static string Serialize(SaveData data) => JsonSerializer.Serialize(data, Options);

    public static SaveData Clone(SaveData data) => JsonSerializer.Deserialize<SaveData>(Serialize(data), Options)!;

    public void Save(SaveData data) => SaveSerialized(Serialize(data));

    public void SaveSerialized(string json)
    {
        if (_blocked) throw new SaveLoadException(_filePath, "запись остановлена; требуется повторная загрузка или восстановление");
        if (!_resolved) Load();
        var bytes = new UTF8Encoding(false).GetBytes(json);
        Deserialize(bytes);
        try
        {
            AssertBaseline();
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            if (File.Exists(_filePath + ".tmp") || Directory.Exists(_filePath + ".tmp"))
                throw new SaveLoadException(_filePath, "обнаружена незавершённая запись");
            // Both the original and proposed checkpoint are durable before replacement.
            // An interrupted first commit can therefore be recovered, never auto-onboarded.
            if (_baseline is not null) History.Preserve(_baseline);
            History.Preserve(bytes);
            WriteDurable(_filePath + ".tmp", bytes);
            History.Prune();
            AssertBaseline();
            File.Move(_filePath + ".tmp", _filePath, overwrite: _baseline is not null);
            _baseline = bytes;
        }
        catch { _blocked = true; throw; }
    }

    private byte[]? ReadPrimary()
    {
        try { return File.ReadAllBytes(_filePath); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    internal void ObserveLegacyBeforeFirstWrite(string path, byte[]? bytes)
    { _firstWriteLegacyPath = path; _firstWriteLegacyBytes = bytes?.ToArray(); }

    private void AssertBaseline()
    {
        if (_baseline is null && _firstWriteLegacyPath is not null)
        {
            byte[]? legacy;
            try { legacy = File.ReadAllBytes(_firstWriteLegacyPath); }
            catch (FileNotFoundException) { legacy = null; }
            catch (DirectoryNotFoundException) { legacy = null; }
            if ((_firstWriteLegacyBytes is null) != (legacy is null) ||
                _firstWriteLegacyBytes is not null && !_firstWriteLegacyBytes.SequenceEqual(legacy!))
                throw new SaveLoadException(_firstWriteLegacyPath, "v1 появился или изменился после загрузки; запись новой игры запрещена");
        }
        var current = ReadPrimary();
        if ((_baseline is null) != (current is null) ||
            _baseline is not null && !current!.SequenceEqual(_baseline))
            throw new SaveLoadException(_filePath, "файл исчез или изменён другим процессом; запись запрещена");
        if (current is not null) Deserialize(current);
    }

    internal static void WriteDurable(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    public void Delete()
    {
        if (!_resolved) Load();
        AssertBaseline();
        if (_baseline is not null) History.Preserve(_baseline);
        if (_baseline is not null) File.Delete(_filePath);
        _blocked = true;
    }

    /// Explicit, confirmed recovery only. Preserve even unreadable current bytes.
    public void RestoreBytes(byte[] bytes, CatalogPackage? catalog = null)
    {
        Deserialize(bytes);
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var current = ReadPrimary();
        if (current is not null)
        {
            var valid = false;
            try
            {
                var displaced = Deserialize(current);
                if (catalog is not null) SaveMigration.ValidateCatalog(displaced, catalog, _filePath);
                valid = true;
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or SaveLoadException)
            { History.Quarantine(current, "rejected"); }
            if (valid) History.Preserve(current);
        }
        History.Preserve(bytes);
        if (File.Exists(_filePath + ".tmp"))
        {
            History.Quarantine(File.ReadAllBytes(_filePath + ".tmp"), "interrupted");
            File.Delete(_filePath + ".tmp");
        }
        var temporary = _filePath + ".restore-" + Guid.NewGuid() + ".tmp";
        WriteDurable(temporary, bytes);
        // Recheck raw bytes here: restore must not replace an unseen external edit either.
        History.Prune();
        var now = ReadPrimary();
        if ((current is null) != (now is null) || current is not null && !current.SequenceEqual(now!))
            throw new SaveLoadException(_filePath, "файл изменился во время восстановления");
        File.Move(temporary, _filePath, overwrite: current is not null);
        _baseline = bytes.ToArray(); _resolved = true; _blocked = false;
    }

    public SaveData? Load() => Load(out _);

    public SaveData? Load(out byte[]? originalBytes)
    {
        originalBytes = null;
        _resolved = false; _blocked = true;
        try
        {
            originalBytes = ReadPrimary();
            if (originalBytes is null)
            {
                if (History.HasEvidence || File.Exists(_filePath + ".tmp") || Directory.Exists(_filePath + ".tmp") ||
                    (Directory.Exists(Path.GetDirectoryName(_filePath)) && Directory.EnumerateFiles(Path.GetDirectoryName(_filePath)!, Path.GetFileName(_filePath) + ".restore-*.tmp").Any()))
                    throw new SaveLoadException(_filePath, "основной файл отсутствует, но найдена история; выберите восстановление");
                _baseline = null; _resolved = true; _blocked = false;
                return null;
            }
            var data = Deserialize(originalBytes);
            _baseline = originalBytes.ToArray(); _resolved = true; _blocked = false;
            return data;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            if (ex is SaveLoadException) throw;
            throw new SaveLoadException(_filePath, ex.Message, ex);
        }
    }

    internal static SaveData Deserialize(byte[] bytes)
    {
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
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
        NormalizeLegacyCare(data!);
        return data!;
    }

    internal static void NormalizeLegacyCare(SaveData data)
    {
        data.Settings.CareMode = CareMode.Chill;
        data.Settings.SuperHardcoreEnabled = false;
        foreach (var care in data.CareStates.Values) care.RanAwayUntilUtc = null;
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
            && data.CareStates is not null && data.PendingEggs is not null && data.Encounters is not null, "обязательное поле равно null");
        Require(data.FeedingTreats >= 0 && data.CatchAccumulatorUnits >= 0 && data.FeedingRemainderUnits >= 0
            && data.ShopCoins >= 0 && data.ShopRemainderUnits >= 0 && data.PendingCatchCount >= 0,
            "отрицательный счётчик");
        Require(data.CatchAccumulatorUnits < ActivityAccumulator.CatchCheckIntervalUnits
            && data.FeedingRemainderUnits < FeedingService.ActivityUnitsPerTreatEarned
            && data.ShopRemainderUnits < ShopService.ActivityUnitsPerCoinEarned, "остаток вне интервала начисления");
        Require(data.SchemaVersion is 1 or 2, "неподдерживаемая версия схемы");
        foreach (var individual in data.Individuals!) ValidateIndividual(individual, data.SchemaVersion);
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
    private static void ValidateIndividual(PokemonIndividual? individual, int schemaVersion)
    {
        Require(individual is not null && individual.Id != Guid.Empty, "неверный экземпляр");
        Require(individual!.SpeciesId > 0 && individual.EvolutionChainId > 0 && individual.ChainLength is >= 1 and <= 3,
            "неверный вид или эволюционная цепочка");
        Require(Enum.IsDefined(individual.Nature) && Enum.IsDefined(individual.CurrentStage)
            && (int)individual.CurrentStage < individual.ChainLength && individual.AccumulatedCarePoints >= 0,
            "неверная стадия или прогресс");
        Require(individual.DisplayStageOverride is null || (individual.DisplayStageOverride >= 0
            && individual.DisplayStageOverride <= (int)individual.CurrentStage), "неверная отображаемая стадия");
        if (schemaVersion != 2) return;
        Require(individual.CurrentSpeciesId > 0 && individual.FamilyRootId > 0 && individual.TotalXp is >= 0 and <= 6400
            && SaveMigration.TraitIds.Contains(individual.FirstTraitId)
            && (individual.SecondTraitId is null || SaveMigration.TraitIds.Contains(individual.SecondTraitId) && individual.SecondTraitId != individual.FirstTraitId)
            && individual.ReachedSpeciesIds is { Count: > 0 } && individual.ParentIds is not null
            && ValidRemainder(individual.XpRemainder)
            && individual.ReachedSpeciesIds[^1] == individual.CurrentSpeciesId
            && (individual.DisplayStageOverride is null || individual.DisplayStageOverride < individual.ReachedSpeciesIds.Count)
            && individual.ReachedSpeciesIds.All(id => id > 0) && individual.ReachedSpeciesIds.Distinct().Count() == individual.ReachedSpeciesIds.Count, "неверный экземпляр v2");
    }

    private static bool ValidRemainder(RationalRemainder? r) =>
        r is not null && r.Denominator > 0 && r.Numerator >= 0 && r.Numerator < r.Denominator;

    private static void ValidateV2(SaveData data)
    {
        Require(!string.IsNullOrWhiteSpace(data.CatalogRevision) && !string.IsNullOrWhiteSpace(data.BalanceRevision)
            && data.MigrationId == SaveMigration.MigrationId, "неверные ревизии v2");
        Require(data.DiscoveredSpeciesIds is not null && data.EvolutionTransitions is not null && data.ShinySpeciesIds is not null
            && data.Metrics is not null && data.AwardLedger is not null && data.Berries is not null && data.Items is not null
            && data.BerryRemainders is not null && data.Tutorial is not null && data.Gift is not null
            && data.CompletedOperations is not null, "обязательное поле v2 равно null");
        Require(SaveMigration.BerryIds.ToHashSet().SetEquals(data.Berries!.Keys) && SaveMigration.ItemIds.ToHashSet().SetEquals(data.Items!.Keys)
            && data.Berries.Values.All(n => n >= 0) && data.Items.Values.All(n => n >= 0) && data.Items["treats"] == data.FeedingTreats, "неверный инвентарь v2");
        Require(SaveMigration.BerryIds.ToHashSet().SetEquals(data.BerryRemainders!.Keys)
            && data.BerryRemainders.Values.All(ValidRemainder) && ValidRemainder(data.CoinRemainder), "неверный дробный остаток");
        Require(data.DiscoveredSpeciesIds!.All(id => id > 0) && data.ShinySpeciesIds!.All(id => id > 0)
            && data.Metrics!.Values.All(n => n is null or >= 0), "неверная метрика v2");
        Require(data.AwardLedger!.Values.All(a => a is not null && a.CoinsGranted >= 0 && a.ClaimStatus is "claimed" or "unclaimed"
            && a.AvailableSpeciesIds is not null), "неверный журнал наград");
        Require(data.Tutorial!.TutorialRevision == SaveMigration.MigrationId && data.Tutorial.CurrentStep is >= 1 and <= 6
            && data.Tutorial.Status is "not-started" or "in-progress" or "completed" or "skipped" && data.Tutorial.SeenHintKeys is not null, "неверный тур");
        Require(data.Gift!.GrantId == "global-update-compatible-pair-v1" && data.Gift.Gifts is not null
            && data.Gift.Gifts.Count is 0 or 2, "неверный подарок");
        var ids = data.Individuals.Select(i => i.Id).ToHashSet();
        Require(data.Encounters!.All(e => e is not null && e.Id != Guid.Empty
            && e.Source is "activity" or "expedition" or "gift"
            && e.Phase is "sealed" or "opened"
            && (e.KnownSpeciesId is null or > 0)
            && (e.GiftIndividualId is null || e.Source == "gift" && e.GiftIndividualId != Guid.Empty)
            && (e.Source != "activity" || e.KnownSpeciesId is null && e.GiftIndividualId is null)
            && (e.Phase == "sealed" && e.Individual is null || e.Phase == "opened" && e.Individual is not null && e.Rarity is not null)), "неверная встреча");
        Require(data.Encounters.Select(e => e.Id).Distinct().Count() == data.Encounters.Count, "повторный ID встречи");
        foreach (var encounter in data.Encounters.Where(e => e.Phase == "opened"))
        {
            ValidateIndividual(encounter.Individual, 2);
            Require(Enum.IsDefined(encounter.Rarity!.Value) && data.Individuals.Any(i => i.Id == encounter.Individual!.Id)
                && (encounter.KnownSpeciesId is null || encounter.Individual!.SpeciesId == encounter.KnownSpeciesId),
                "открытая встреча без экземпляра");
        }
        Require(data.Encounters.Where(e => e.Phase == "opened").Select(e => e.Individual!.Id).Distinct().Count()
            == data.Encounters.Count(e => e.Phase == "opened"), "экземпляр выдан дважды");
        foreach (var gift in data.Gift.Gifts!)
        {
            Require(gift is not null && gift.Status is "reserved" or "opened" && gift.Individual is not null, "неверная выдача");
            ValidateIndividual(gift!.Individual, 2);
            Require(gift!.Status != "reserved" || ids.Add(gift.Individual!.Id), "повторный GUID подарка");
        }
        Require(data.Encounters.All(e => e.GiftIndividualId is not { } giftId || data.Gift.Gifts.Any(g =>
            g.Individual.Id == giftId && g.Status == (e.Phase == "sealed" ? "reserved" : "opened")
            && (e.Individual is null || e.Individual.Id == giftId))), "встреча не совпадает с подарком");
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
