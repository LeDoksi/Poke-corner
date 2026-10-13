namespace PokeCorner.Core.Domain;

public enum ShinySource { Activity, Expedition, Nursery, Starter }

// The roll and its effective chance travel with a proposed saved outcome. Replaying a saved
// outcome must use that draw, never the current stock, luck or newly installed assets.
public readonly record struct ShinyDraw(double Roll, double Chance);

public static class ShinyRules
{
    public const double BaseChance = 1d / 512;
    public const double CharmMultiplier = 4;
    public const double NurseryLuckStep = .0025;
    public const double NurseryChanceCap = .1;

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
        return isShiny ? 0 : Math.Min(NurseryChanceCap - BaseChance, currentBonus + NurseryLuckStep);
    }
}
