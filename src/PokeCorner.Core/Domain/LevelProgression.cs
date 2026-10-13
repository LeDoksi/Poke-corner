using System.Text.Json;
namespace PokeCorner.Core.Domain;

public sealed class LevelProgression
{
    private readonly int[] thresholds;
    private readonly int[] tierLevels;
    private readonly int secondTraitLevel;
    private readonly TraitRules traits;

    public LevelProgression(string dataRoot)
    {
        using var balance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dataRoot, "final", "balance.json")));
        var root = balance.RootElement;
        thresholds = root.GetProperty("levels").EnumerateArray().Select((row, index) =>
        {
            if (row.GetProperty("level").GetInt32() != index + 1) throw new InvalidDataException("Invalid level order.");
            return row.GetProperty("totalXp").GetInt32();
        }).ToArray();
        tierLevels = root.GetProperty("traitTiersAt").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        secondTraitLevel = root.GetProperty("secondTraitLevel").GetInt32();
        if (root.GetProperty("maxLevel").GetInt32() != 40 || thresholds.Length != 40 || thresholds[0] != 0 ||
            thresholds.Zip(thresholds.Skip(1)).Any(pair => pair.Second <= pair.First) ||
            thresholds[^1] != root.GetProperty("maxXp").GetInt32() ||
            tierLevels.Length != 3 || tierLevels[0] != 1 || tierLevels[1] != 20 || tierLevels[2] != 30 ||
            secondTraitLevel != 10)
            throw new InvalidDataException("Invalid progression balance.");
        traits = new TraitRules(dataRoot);
    }

    public int MaxXp => thresholds[^1];

    public int XpForLevel(int level)
    {
        if (level is < 1 or > 40) throw new ArgumentOutOfRangeException(nameof(level));
        return thresholds[level - 1];
    }

    public int LevelForXp(int xp)
    {
        if (xp < 0 || xp > MaxXp) throw new ArgumentOutOfRangeException(nameof(xp));
        var index = Array.BinarySearch(thresholds, xp);
        return index >= 0 ? index + 1 : ~index;
    }

    public int TraitTierForXp(int xp)
    {
        var level = LevelForXp(xp);
        return level >= tierLevels[2] ? 3 : level >= tierLevels[1] ? 2 : 1;
    }

    public int AddXp(PokemonIndividual individual, int xp, Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(individual);
        if (xp < 0) throw new ArgumentOutOfRangeException(nameof(xp));
        var current = individual.TotalXp;
        LevelForXp(current);
        if (string.IsNullOrWhiteSpace(individual.FirstTraitId)) throw new InvalidOperationException("First trait is missing.");
        var gained = (int)Math.Min((long)xp, MaxXp - current);
        var next = current + gained;
        string? second = null;
        if (next >= XpForLevel(secondTraitLevel) && individual.SecondTraitId is null)
        {
            var allowed = traits.Allowed(individual.CurrentSpeciesId).Where(id => id != individual.FirstTraitId).ToArray();
            if (allowed.Length == 0) throw new InvalidOperationException("No second trait is allowed.");
            second = allowed[(random ?? Random.Shared).Next(allowed.Length)];
        }
        individual.TotalXp = next;
        if (second is not null) individual.SecondTraitId = second;
        return gained;
    }
}
