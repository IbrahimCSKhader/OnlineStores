using onlineStore.Services.Rewards;

namespace onlineStore.Tests;

public sealed class PurchasePointsPolicyTests
{
    [Fact]
    public void CalculatePoints_AwardsFivePointsPerPurchasedProductQuantity()
    {
        var points = PurchasePointsPolicy.CalculatePoints([3, 5]);

        Assert.Equal(40, points);
    }

    [Fact]
    public void CalculatePoints_IgnoresZeroAndNegativeQuantities()
    {
        var points = PurchasePointsPolicy.CalculatePoints([2, 0, -4, 1]);

        Assert.Equal(15, points);
    }

    [Fact]
    public void CalculatePoints_ReturnsZeroWhenQuantitiesAreMissing()
    {
        var points = PurchasePointsPolicy.CalculatePoints(null!);

        Assert.Equal(0, points);
    }
}
