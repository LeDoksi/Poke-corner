using System.Text.Json;

namespace PokeCorner.Core.Persistence;

public sealed class JsonSaveStore : ISaveStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public JsonSaveStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    public void Save(SaveData data)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write to a temp file first, then atomically move it into place: a crash mid-write
        // leaves either the old file intact or the new file complete, never a truncated one.
        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(data, Options));
        File.Move(tempPath, _filePath, overwrite: true);
    }

    public SaveData? Load()
    {
        if (!File.Exists(_filePath)) return null;
        var json = File.ReadAllText(_filePath);
        try
        {
            return JsonSerializer.Deserialize<SaveData>(json, Options);
        }
        catch (JsonException)
        {
            // Treat a corrupted save file the same as a missing one — callers (e.g.
            // FirstRunFlow.NeedsOnboarding) already handle null correctly.
            return null;
        }
    }
}
