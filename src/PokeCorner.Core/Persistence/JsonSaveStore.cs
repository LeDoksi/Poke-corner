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

    public SaveData? Load()
    {
        try
        {
            var json = File.ReadAllText(_filePath);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Require(root.ValueKind == JsonValueKind.Object, "корень JSON должен быть объектом");
            if (root.TryGetProperty("SchemaVersion", out var version))
                Require(version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var number) && number == 1,
                    "неподдерживаемая версия схемы");

            // Id has a NewGuid initializer for NEW individuals/eggs; a missing saved identity
            // must be rejected before deserialization can silently replace it.
            var identities = new HashSet<Guid>();
            ValidateIdentities(root, "Individuals", identities, required: true);
            ValidateIdentities(root, "PendingEggs", identities, required: false);
            var data = JsonSerializer.Deserialize<SaveData>(json, Options);
            Require(data is not null, "пустое сохранение");
            Validate(data!);
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

    private static void Validate(SaveData data)
    {
        Require(data.Individuals is not null && data.Settings is not null
            && data.CareStates is not null && data.PendingEggs is not null, "обязательное поле равно null");
        Require(data.FeedingTreats >= 0 && data.CatchAccumulatorUnits >= 0 && data.FeedingRemainderUnits >= 0
            && data.ShopCoins >= 0 && data.ShopRemainderUnits >= 0 && data.PendingCatchCount >= 0,
            "отрицательный счётчик");
        Require(data.CatchAccumulatorUnits < ActivityAccumulator.CatchCheckIntervalUnits
            && data.FeedingRemainderUnits < FeedingService.ActivityUnitsPerTreatEarned
            && data.ShopRemainderUnits < ShopService.ActivityUnitsPerCoinEarned, "остаток вне интервала начисления");
        foreach (var individual in data.Individuals!)
        {
            Require(individual.SpeciesId > 0 && individual.EvolutionChainId > 0 && individual.ChainLength is >= 1 and <= 3,
                "неверный вид или эволюционная цепочка");
            Require(Enum.IsDefined(individual.Nature) && Enum.IsDefined(individual.CurrentStage)
                && (int)individual.CurrentStage < individual.ChainLength && individual.AccumulatedCarePoints >= 0,
                "неверная стадия или прогресс");
            Require(individual.DisplayStageOverride is null || (individual.DisplayStageOverride >= 0
                && individual.DisplayStageOverride <= (int)individual.CurrentStage), "неверная отображаемая стадия");
        }
        Require(data.ActiveIndividualId is null || data.Individuals!.Any(i => i.Id == data.ActiveIndividualId),
            "активный GUID отсутствует в коллекции");
        Require(data.CareStates!.All(pair => pair.Key != Guid.Empty && pair.Value is not null), "неверное состояние ухода");
        Require(data.PendingEggs!.All(egg => Enum.IsDefined(egg.Tier)), "неверная редкость яйца");
        Require(Enum.IsDefined(data.Settings!.CareMode) && data.Settings.MonitorIndex >= 0
            && data.Settings.PetSizePixels > 0 && data.Settings.CrySoundVolume is >= 0 and <= 100,
            "неверные настройки");
    }
}
