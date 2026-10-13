namespace PokeCorner.Core.Domain;

// Fixed hourly rates are scaled once, so repeated short trips carry exactly the same remainder.
public sealed class ExpeditionRewards
{
    public const long Denominator = 60_000_000_000;
    public string PrimaryBerryId { get; set; } = "";
    public long PrimaryNumerator { get; set; }
    public long CoinNumerator { get; set; }
    public long XpNumerator { get; set; }
    public Dictionary<string, int> ExtraBerries { get; set; } = [];
    public Dictionary<string, int> ExtraItems { get; set; } = [];
    public int ExtraCoins { get; set; }
    public bool HasEncounter { get; set; }
}
