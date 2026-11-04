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

    public static double NextNurseryLuck(double currentBonus, bool isShiny, bool variantSupported)
    {
        if (!double.IsFinite(currentBonus) || currentBonus < 0) throw new ArgumentOutOfRangeException(nameof(currentBonus));
        if (!variantSupported) return currentBonus;
        return isShiny ? 0 : Math.Min(NurseryLuckMax, currentBonus + NurseryLuckStep);
    }
}
