namespace PokeCorner.Core.Domain;

public enum ShinySource { Activity, Expedition, Nursery, Starter }

// The roll and its effective chance travel with a proposed saved outcome. Replaying a saved
// outcome must use that draw, never the current stock, luck or newly installed assets.
public readonly record struct ShinyDraw(double Roll, double Chance);

public static class ShinyRules
{
    // Ш-А (Georgy 04.11): 15% for every source, so collecting shiny forms is a pleasure, not a grind.
    public const double BaseChance = .15;
    // The shiny charm guarantees a shiny on the attempt it is spent on (any chance times this reaches 1).
    public const double CharmMultiplier = 1 / BaseChance;
    // D-34 (Georgy 04.11): ten visible steps of +3%, so the bar moves on every eligible hatch.
    public const double NurseryLuckStep = .03;
    public const int NurseryLuckSteps = 10;
    public const double NurseryLuckMax = NurseryLuckStep * NurseryLuckSteps;
    public const double NurseryChanceCap = BaseChance + NurseryLuckMax;
    // The scale before D-34: +0.25% per hatch up to 10%. Old saves are converted step for step.
    public const double LegacyNurseryLuckStep = .0025;

    public static int NurseryLuckStepsOf(double luck) =>
        Math.Clamp((int)Math.Round(luck / NurseryLuckStep, MidpointRounding.AwayFromZero), 0, NurseryLuckSteps);

    public static double ConvertLegacyNurseryLuck(double legacyLuck) =>
        !double.IsFinite(legacyLuck) || legacyLuck <= 0 ? 0
        : Math.Min(NurseryLuckSteps, (int)Math.Round(legacyLuck / LegacyNurseryLuckStep, MidpointRounding.AwayFromZero)) * NurseryLuckStep;

    public static double Chance(ShinySource source, double nurseryLuck, bool charm, bool variantSupported)
    {
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source));
        if (!double.IsFinite(nurseryLuck) || nurseryLuck < 0) throw new ArgumentOutOfRangeException(nameof(nurseryLuck));
        if (!variantSupported) return 0;
        var chance = source == ShinySource.Nursery
            ? Math.Min(NurseryChanceCap, BaseChance + nurseryLuck)
            : BaseChance;
        return Math.Min(1, chance * (charm ? CharmMultiplier : 1));
    }

    public static bool Resolve(ShinySource source, ShinyDraw persistedDraw)
    {
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source));
        if (!double.IsFinite(persistedDraw.Roll) || persistedDraw.Roll is < 0 or >= 1 ||
            !double.IsFinite(persistedDraw.Chance) || persistedDraw.Chance is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(persistedDraw));
        return persistedDraw.Roll < persistedDraw.Chance;
    }

    /// D-41 (Georgy 11.11): shine belongs to the family. Once a shiny of any stage has been owned, the shiny form
    /// counts as open for every stage of that family the player has already seen, so nobody is raised twice.
    /// Stages never seen stay closed, and so do stages that have no shiny art. Achievements keep counting the
    /// raw set (only shiny forms actually owned).
    public static IReadOnlySet<int> FamilyShiny(IReadOnlySet<int> ownedShiny, IEnumerable<int> seen,
        IReadOnlyDictionary<int, int> familyRoots, Func<int, bool>? shinySupported = null)
    {
        if (ownedShiny.Count == 0) return ownedShiny;
        int Root(int id) => familyRoots.TryGetValue(id, out var root) && root > 0 ? root : id;
        var families = ownedShiny.Select(Root).ToHashSet();
        var open = ownedShiny.ToHashSet();
        foreach (var id in seen)
            if (families.Contains(Root(id)) && (shinySupported?.Invoke(id) ?? true)) open.Add(id);
        return open;
    }

    public static double NextNurseryLuck(double currentBonus, bool isShiny, bool variantSupported)
    {
        if (!double.IsFinite(currentBonus) || currentBonus < 0) throw new ArgumentOutOfRangeException(nameof(currentBonus));
        if (!variantSupported) return currentBonus;
        return isShiny ? 0 : Math.Min(NurseryLuckMax, currentBonus + NurseryLuckStep);
    }
}
