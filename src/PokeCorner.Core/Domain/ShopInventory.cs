namespace PokeCorner.Core.Domain;

public sealed class ShopInventory
{
    public int Coins { get; set; }
    public long RemainderUnits { get; set; }
    public bool CatchLuckyCharmActive { get; set; }
}
