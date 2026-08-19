using System.Reflection;
using System.Text.Json;

namespace PokeCorner.Core.PokeApi;

public sealed class SpriteCache
{
    private readonly string _cacheDirectory;

    public SpriteCache(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<byte[]> GetOrFetchAsync(int speciesId, Func<Task<byte[]>> fetch)
    {
        var path = Path.Combine(_cacheDirectory, $"{speciesId}.png");
        if (File.Exists(path))
        {
            return await File.ReadAllBytesAsync(path);
        }

        var bytes = await fetch();
        await File.WriteAllBytesAsync(path, bytes);
        return bytes;
    }

    public IReadOnlyList<Domain.SpeciesEvolutionChain> LoadOfflineStarters()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("PokeCorner.Core.PokeApi.OfflineStarterFallback.json")
            ?? throw new InvalidOperationException("OfflineStarterFallback.json embedded resource not found.");
        return JsonSerializer.Deserialize<List<Domain.SpeciesEvolutionChain>>(stream)!;
    }
}
