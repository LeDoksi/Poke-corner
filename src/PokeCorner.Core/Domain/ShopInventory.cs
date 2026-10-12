namespace PokeCorner.Core.Domain;

public sealed class ShopInventory
{
    public int Coins { get; set; }
    public long RemainderUnits { get; set; }
    public bool CatchLuckyCharmActive { get; set; }
    public Dictionary<string, int> Berries { get; } = [];
    public Dictionary<string, int> Items { get; } = [];
}
