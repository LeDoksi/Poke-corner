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

    [Fact]
    public void ConsumeCatchLuckyCharm_SpendsOnlyOnce()
    {
        var shop = new ShopInventory { CatchLuckyCharmActive = true };
        var service = new ShopService();
        Assert.Equal(4.0, service.ConsumeCatchLuckyCharm(shop));
        Assert.Equal(1.0, service.ConsumeCatchLuckyCharm(shop));
        Assert.False(shop.CatchLuckyCharmActive);
    }
}
