using System.Security.Cryptography;
using System.Text.Json;

namespace PokeCorner.Core.Persistence;

public sealed record SaveHistoryEntry(string Path, DateTimeOffset SavedAtUtc, int SchemaVersion, int CollectionCount, int Coins, bool IsLegacy = false, string? Sha256 = null, bool FromMemory = false)
{
    public override string ToString() => $"{SavedAtUtc.ToLocalTime():dd.MM.yyyy HH:mm:ss} · {CollectionCount} покемонов · {Coins} монет · v{SchemaVersion}" + (FromMemory ? " · из памяти (не основной файл)" : "");
}

/// Small, self-contained checkpoints: original bytes, SHA256 and timestamp.
public sealed class SaveHistory
{
    private readonly string _primary;
    private readonly Func<DateTimeOffset> _clock;
    public string DirectoryPath => _primary + ".history";
    public bool HasEvidence => File.Exists(DirectoryPath) || Directory.Exists(DirectoryPath);
    public int UnreadableCount { get; private set; }
    private sealed record Checkpoint(DateTimeOffset SavedAtUtc, string Sha256, byte[] Bytes, bool FromMemory = false);
    public SaveHistory(string primary, Func<DateTimeOffset>? clock = null)
    { _primary = primary; _clock = clock ?? (() => DateTimeOffset.UtcNow); }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public void Capture(SaveData data)
    {
        Preserve(System.Text.Encoding.UTF8.GetBytes(JsonSaveStore.Serialize(data)), fromMemory: true);
        Prune();
    }

    internal void Preserve(byte[] bytes, bool fromMemory = false)
    {
        var data = JsonSaveStore.Deserialize(bytes);
        Directory.CreateDirectory(DirectoryPath);
        var now = _clock().ToUniversalTime();
        var hash = Hash(bytes);
        var checkpoint = JsonSerializer.SerializeToUtf8Bytes(new Checkpoint(now, hash, bytes, fromMemory));
        PreserveOnce(Path.Combine(DirectoryPath, "initial.checkpoint"), checkpoint);
        var day = Path.Combine(DirectoryPath, $"day-{now:yyyy-MM-dd}.checkpoint");
        if (!File.Exists(day)) PreserveOnce(day, checkpoint);
        else
        {
            var previous = ReadCheckpoint(day);
            var previousCount = JsonSaveStore.Deserialize(previous.Bytes).Individuals.Count;
            if (data.Individuals.Count > previousCount || data.Individuals.Count == previousCount && now >= previous.SavedAtUtc)
            {
                var temp = day + ".tmp-" + Guid.NewGuid();
                JsonSaveStore.WriteDurable(temp, checkpoint);
                File.Move(temp, day, overwrite: true);
            }
        }
        var existingPath = Directory.EnumerateFiles(DirectoryPath, $"recent-*-{hash}.checkpoint").FirstOrDefault();
        if (existingPath is null)
            PreserveOnce(Path.Combine(DirectoryPath, $"recent-{now.UtcTicks:D19}-{hash}.checkpoint"), checkpoint);
        else if (fromMemory && !ReadCheckpoint(existingPath).FromMemory)
        {
            // A failed autosave may already contain these bytes; label the live recovery copy.
            var temp = existingPath + ".tmp-" + Guid.NewGuid();
            JsonSaveStore.WriteDurable(temp, checkpoint);
            File.Move(temp, existingPath, overwrite: true);
        }
    }

    private static void PreserveOnce(string path, byte[] bytes)
    {
        if (File.Exists(path)) { ReadCheckpoint(path); return; }
        var temp = path + ".tmp-" + Guid.NewGuid();
        JsonSaveStore.WriteDurable(temp, bytes);
        File.Move(temp, path);
    }

    internal void Quarantine(byte[] bytes, string kind)
    {
        var path = _primary + "." + kind + "-" + Hash(bytes);
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).SequenceEqual(bytes)) throw new IOException("Карантин не совпадает с исходными байтами.");
            return;
        }
        JsonSaveStore.WriteDurable(path, bytes);
    }

    private static Checkpoint ReadCheckpoint(string path)
    {
        var checkpoint = JsonSerializer.Deserialize<Checkpoint>(File.ReadAllBytes(path))
            ?? throw new InvalidDataException("Пустая резервная копия.");
        if (checkpoint.Bytes is null || Hash(checkpoint.Bytes) != checkpoint.Sha256)
            throw new InvalidDataException("SHA256 резервной копии не совпадает.");
        JsonSaveStore.Deserialize(checkpoint.Bytes);
        return checkpoint;
    }

    public byte[] ReadValidated(SaveHistoryEntry entry)
    {
        try
        {
            var bytes = entry.IsLegacy ? ReadLegacy(entry.Path) : ReadCheckpoint(entry.Path).Bytes;
            if (entry.Sha256 is not null && entry.Sha256 != Hash(bytes))
                throw new InvalidDataException("Копия изменилась после выбора в истории.");
            return bytes;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException or KeyNotFoundException)
        { throw new SaveLoadException(entry.Path, ex.Message, ex); }
    }

    private static byte[] ReadLegacy(string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "manifest.json")));
        var bytes = File.ReadAllBytes(path);
        var lookup = File.ReadAllBytes(Path.Combine(directory, LegacyLookup.PreservedLookupFileName));
        if (manifest.RootElement.GetProperty("StateSha256").GetString() != Hash(bytes) ||
            manifest.RootElement.GetProperty("LookupSha256").GetString() != Hash(lookup))
            throw new InvalidDataException("SHA256 сохранения или lookup v1 не совпадает.");
        if (JsonSaveStore.Deserialize(bytes).SchemaVersion != 1) throw new InvalidDataException("Ожидалась схема v1.");
        return bytes;
    }

    public IReadOnlyList<SaveHistoryEntry> List()
    {
        UnreadableCount = 0;
        var entries = new Dictionary<string, SaveHistoryEntry>();
        if (Directory.Exists(DirectoryPath))
            foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.checkpoint"))
            {
                try
                {
                    var checkpoint = ReadCheckpoint(path); var data = JsonSaveStore.Deserialize(checkpoint.Bytes);
                    var entry = new SaveHistoryEntry(path, checkpoint.SavedAtUtc, data.SchemaVersion, data.Individuals.Count, data.ShopCoins,
                        Sha256: checkpoint.Sha256, FromMemory: checkpoint.FromMemory);
                    if (checkpoint.FromMemory) entries[checkpoint.Sha256] = entry;
                    else entries.TryAdd(checkpoint.Sha256, entry);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) { UnreadableCount++; }
            }
        var legacy = Path.Combine(Path.GetDirectoryName(_primary)!, "legacy-v1");
        if (Directory.Exists(legacy))
            foreach (var path in Directory.EnumerateFiles(legacy, "state.json", SearchOption.AllDirectories))
            {
                try
                {
                    var bytes = ReadLegacy(path); var data = JsonSaveStore.Deserialize(bytes);
                    entries.TryAdd(Hash(bytes), new(path, File.GetLastWriteTimeUtc(path), 1, data.Individuals.Count, data.ShopCoins, true, Hash(bytes)));
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or KeyNotFoundException) { UnreadableCount++; }
            }
        return entries.Values.OrderByDescending(e => e.SavedAtUtc).ToArray();
    }

    internal void Prune()
    {
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "recent-*.checkpoint").OrderByDescending(Path.GetFileName).Skip(20))
            File.Delete(path);
        var oldestDay = _clock().UtcDateTime.Date.AddDays(-29);
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "day-*.checkpoint"))
            if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(path)[4..], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var day) && day < oldestDay) File.Delete(path);
    }
}
