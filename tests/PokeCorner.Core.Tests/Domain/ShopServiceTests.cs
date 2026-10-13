using PokeCorner.Core.Domain;

namespace PokeCorner.Core.Tests.Domain;

public class ShopServiceTests
{
    [Fact]
    public void EarnCoinsFromActivity_ConvertsUnitsAtFixedRate()
    {
        var shop = new ShopInventory();
        new ShopService().EarnCoinsFromActivity(shop, ShopService.ActivityUnitsPerCoinEarned * 4 + 50);
        Assert.Equal(4, shop.Coins);
        Assert.Equal(50, shop.RemainderUnits);
    }

}
