using System.Text.Json;

namespace PokeCorner.Core.PokeApi;

public sealed class PokeApiClient
{
    private readonly HttpClient _httpClient;

    public PokeApiClient(HttpMessageHandler handler)
    {
        _httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://pokeapi.co/api/v2/") };
    }

    public async Task<Domain.SpeciesEvolutionChain> FetchEvolutionChainAsync(int chainId)
    {
        var json = await _httpClient.GetStringAsync($"evolution-chain/{chainId}/");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var chainNode = root.GetProperty("chain");

        var speciesUrl = chainNode.GetProperty("species").GetProperty("url").GetString()!;
        var starterSpeciesId = ExtractIdFromUrl(speciesUrl);

        // capture_rate lives on the species resource (pokemon-species/{id}/), not on the
        // evolution-chain resource, so it takes a second call.
        var captureRate = await FetchCaptureRateAsync(starterSpeciesId);
        var stages = CollectStages(chainNode);

        // Types are NOT on the evolution-chain resource (nor on pokemon-species), so unlike the
        // names above they cost one pokemon/{id}/ call per stage. That is ~1 extra request per
        // species in a full catalog refresh; the alternative — shipping chains without types —
        // would leave every badge blank for anyone whose catalog re-crawls.
        var stageTypes = new List<IReadOnlyList<string>>();
        foreach (var stage in stages)
        {
            stageTypes.Add(await FetchTypesAsync(stage.Id));
        }

        return new Domain.SpeciesEvolutionChain(
            ChainId: root.GetProperty("id").GetInt32(),
            StarterSpeciesId: starterSpeciesId,
            CaptureRate: captureRate,
            ChainLength: stages.Count,
            StageSpeciesIds: stages.Select(stage => stage.Id).ToList(),
            StageSpeciesNames: stages.Select(stage => stage.Name).ToList(),
            StageTypes: stageTypes);
    }

    /// PokeAPI type names for one species, in slot order ("bug" then "flying" for Butterfree).
    /// Reads the SAME pokemon/{id}/ resource <see cref="FetchAnimatedSpriteAsync"/> already
    /// uses — the types array sits alongside the sprites node in that one response body.
    ///
    /// Returns an empty list rather than throwing when the property is missing or malformed:
    /// a species with no readable types must degrade to "no badges", not fail a whole crawl.
    public async Task<IReadOnlyList<string>> FetchTypesAsync(int speciesId)
    {
        var json = await _httpClient.GetStringAsync($"pokemon/{speciesId}/");
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("types", out var types) || types.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return types.EnumerateArray()
            .Select(entry => (
                Slot: entry.TryGetProperty("slot", out var slot) && slot.ValueKind == JsonValueKind.Number ? slot.GetInt32() : 0,
                Name: entry.TryGetProperty("type", out var type) && type.TryGetProperty("name", out var name)
                    ? name.GetString()
                    : null))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .OrderBy(entry => entry.Slot)
            .Select(entry => entry.Name!)
            .ToList();
    }

    public async Task<int> FetchCaptureRateAsync(int speciesId)
    {
        var json = await _httpClient.GetStringAsync($"pokemon-species/{speciesId}/");
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("capture_rate").GetInt32();
    }

    public async Task<int> FetchEvolutionChainIdForSpeciesAsync(int speciesId)
    {
        var json = await _httpClient.GetStringAsync($"pokemon-species/{speciesId}/");
        using var doc = JsonDocument.Parse(json);
        var chainUrl = doc.RootElement.GetProperty("evolution_chain").GetProperty("url").GetString()!;
        return ExtractIdFromUrl(chainUrl);
    }

    public async Task<byte[]> FetchSpriteAsync(int speciesId)
    {
        return await _httpClient.GetByteArrayAsync(
            $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{speciesId}.png");
    }

    public async Task<byte[]?> FetchAnimatedSpriteAsync(int speciesId)
    {
        var json = await _httpClient.GetStringAsync($"pokemon/{speciesId}/");
        using var doc = JsonDocument.Parse(json);
        var animatedUrl = doc.RootElement
            .GetProperty("sprites").GetProperty("versions")
            .GetProperty("generation-v").GetProperty("black-white")
            .GetProperty("animated").GetProperty("front_default");

        if (animatedUrl.ValueKind == JsonValueKind.Null) return null;

        return await _httpClient.GetByteArrayAsync(animatedUrl.GetString());
    }

    private static int ExtractIdFromUrl(string url)
    {
        var lastSegment = url.TrimEnd('/').Split('/')[^1];
        if (!int.TryParse(lastSegment, out var id))
        {
            throw new InvalidOperationException($"Could not extract a numeric id from URL: {url}");
        }
        return id;
    }

    /// <summary>
    /// Walks the evolution chain tree and collects the species id AND display name at each
    /// stage along the longest branch (mirroring the branch-picking behavior of the
    /// chain-length computation), so the returned list's length always matches the resulting
    /// ChainLength. The name is already present on every node of the evolution-chain JSON
    /// we're fetching anyway, so capturing it costs no extra HTTP calls.
    /// </summary>
    private static List<(int Id, string Name)> CollectStages(JsonElement chainNode)
    {
        var speciesNode = chainNode.GetProperty("species");
        var speciesId = ExtractIdFromUrl(speciesNode.GetProperty("url").GetString()!);
        var speciesName = ToDisplayName(speciesNode.GetProperty("name").GetString());
        var self = (speciesId, speciesName);

        if (!chainNode.TryGetProperty("evolves_to", out var evolvesTo) || evolvesTo.GetArrayLength() == 0)
        {
            return new List<(int, string)> { self };
        }

        List<(int Id, string Name)>? longestChildPath = null;
        foreach (var child in evolvesTo.EnumerateArray())
        {
            var childPath = CollectStages(child);
            if (longestChildPath is null || childPath.Count > longestChildPath.Count)
            {
                longestChildPath = childPath;
            }
        }

        var result = new List<(int Id, string Name)> { self };
        result.AddRange(longestChildPath!);
        return result;
    }

    /// <summary>
    /// PokeAPI species names are lowercase-hyphenated ("mr-mime", "nidoran-f"). Turn them into
    /// something presentable ("Mr Mime", "Nidoran F") without maintaining a per-species table.
    /// </summary>
    internal static string ToDisplayName(string? apiName)
    {
        if (string.IsNullOrWhiteSpace(apiName)) return string.Empty;

        var words = apiName.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant());
        return string.Join(' ', words);
    }
}
