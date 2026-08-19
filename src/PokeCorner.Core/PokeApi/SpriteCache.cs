using System.Reflection;
using System.Text.Json;

namespace PokeCorner.Core.PokeApi;

public sealed class SpriteCache
{
    private readonly string _cacheDirectory;
    private IReadOnlyList<Domain.SpeciesEvolutionChain>? _cachedOfflineStarters;

    public SpriteCache(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<byte[]?> GetOrFetchAsync(int speciesId, Func<Task<byte[]>> fetch)
    {
        var path = Path.Combine(_cacheDirectory, $"{speciesId}.png");
        if (File.Exists(path))
        {
            return await File.ReadAllBytesAsync(path);
        }

        byte[] bytes;
        try
        {
            bytes = await fetch();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Fresh offline first launch (or a network blip): degrade gracefully instead of
            // crashing the caller. There's nothing to cache, so leave the disk untouched.
            return null;
        }

        if (bytes.Length == 0)
        {
            // Treat an empty response the same as a failed fetch — don't cache a zero-byte file.
            return null;
        }

        await File.WriteAllBytesAsync(path, bytes);
        return bytes;
    }

    public IReadOnlyList<Domain.SpeciesEvolutionChain> LoadOfflineStarters()
    {
        if (_cachedOfflineStarters is not null)
        {
            return _cachedOfflineStarters;
        }

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("PokeCorner.Core.PokeApi.OfflineStarterFallback.json")
            ?? throw new InvalidOperationException("OfflineStarterFallback.json embedded resource not found.");
        var starters = JsonSerializer.Deserialize<List<Domain.SpeciesEvolutionChain>>(stream)!;
        _cachedOfflineStarters = starters;
        return starters;
    }
}
