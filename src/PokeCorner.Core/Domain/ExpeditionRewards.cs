namespace PokeCorner.Core.Domain;

public sealed class ExpeditionRewards
{
    public string PrimaryBerryId { get; set; } = "";
    public long PrimaryNumerator { get; set; }
    public long PrimaryDenominator { get; set; } = 1;
    public long CoinNumerator { get; set; }
    public long CoinDenominator { get; set; } = 1;
    public long XpNumerator { get; set; }
    public long XpDenominator { get; set; } = 1;
    public Dictionary<string, int> ExtraBerries { get; set; } = [];
    public Dictionary<string, int> ExtraItems { get; set; } = [];
    public int ExtraCoins { get; set; }
    public bool HasEncounter { get; set; }
}
