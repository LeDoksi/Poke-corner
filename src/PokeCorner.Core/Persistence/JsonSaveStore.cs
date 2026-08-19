using System.Text.Json;

namespace PokeCorner.Core.Persistence;

public sealed class JsonSaveStore : ISaveStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public JsonSaveStore(string filePath) => _filePath = filePath;

    public void Save(SaveData data)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(_filePath, JsonSerializer.Serialize(data, Options));
    }

    public SaveData? Load()
    {
        if (!File.Exists(_filePath)) return null;
        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<SaveData>(json, Options);
    }
}
