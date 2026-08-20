namespace PokeCorner.Core.Domain;

public enum RarityTier { Common, Uncommon, Rare, Legendary }

public static class RarityClassifier
{
    public static RarityTier Classify(int captureRate) => captureRate switch
    {
        >= 150 => RarityTier.Common,
        >= 70 => RarityTier.Uncommon,
        >= 30 => RarityTier.Rare,
        _ => RarityTier.Legendary
    };
}
