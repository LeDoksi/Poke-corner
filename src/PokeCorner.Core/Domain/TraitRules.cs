using System.Text.Json;
using PokeCorner.Core.PokeApi;
namespace PokeCorner.Core.Domain;

public sealed class TraitRules
{
    private readonly Dictionary<string, decimal[]> values;
    private readonly HashSet<int> evolvable;
    private readonly Dictionary<int, int[]> nextSpecies;

    public TraitRules(string dataRoot)
    {
        using var rules = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dataRoot, "final", "catalog-rules.json")));
        using var balance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dataRoot, "final", "balance.json")));
        nextSpecies = rules.RootElement.GetProperty("evolutionEdges").EnumerateArray()
            .GroupBy(edge => edge.GetProperty("from").GetInt32())
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.GetProperty("to").GetInt32()).ToArray());
        evolvable = nextSpecies.Keys.ToHashSet();
        values = balance.RootElement.GetProperty("traits").EnumerateObject()
            .ToDictionary(item => item.Name, item => item.Value.EnumerateArray().Select(v => v.GetDecimal()).ToArray());
        if (values.Count != 11 || values.Values.Any(v => v.Length != 3 || v.Any(x => x < 0)))
            throw new InvalidDataException("Invalid trait balance.");
    }

    public IReadOnlyList<string> Allowed(int speciesId)
    {
        if (speciesId <= 0) throw new ArgumentOutOfRangeException(nameof(speciesId));
        return values.Keys.Where(id => id != "fast-grower" || evolvable.Contains(speciesId)).ToArray();
    }

    public IReadOnlyList<string> Allowed(int speciesId, bool isShiny, CatalogPackage catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (speciesId <= 0) throw new ArgumentOutOfRangeException(nameof(speciesId));
        var canEvolve = nextSpecies.TryGetValue(speciesId, out var next) &&
            next.Any(id => catalog.CanObtain(id, isShiny));
        return values.Keys.Where(id => id != "fast-grower" || canEvolve).ToArray();
    }

    public decimal Value(string traitId, int tier)
    {
        if (!values.TryGetValue(traitId, out var tiers)) throw new ArgumentException("Unknown trait.", nameof(traitId));
        if (tier is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(tier));
        return tiers[tier - 1];
    }

    public static decimal Stack(IEnumerable<decimal> effects)
    {
        var ranked = effects.OrderByDescending(x => x).ToArray();
        if (ranked.Any(x => x < 0)) throw new ArgumentOutOfRangeException(nameof(effects));
        return ranked.Take(3).Select((x, index) => x * (index switch { 0 => 1m, 1 => 0.5m, _ => 0.25m })).Sum();
    }

    internal static string[] Allowed(int speciesId, JsonElement rules, JsonElement balance)
    {
        var canEvolve = rules.GetProperty("evolutionEdges").EnumerateArray().Any(e => e.GetProperty("from").GetInt32() == speciesId);
        return balance.GetProperty("traits").EnumerateObject().Select(p => p.Name)
            .Where(id => canEvolve || id != "fast-grower").ToArray();
    }
}
