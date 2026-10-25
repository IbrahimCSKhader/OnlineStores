using Microsoft.Extensions.Logging.Abstractions;
using onlineStore.DTOs.Coupon;
using onlineStore.Services.Coupon;

namespace onlineStore.Tests;

public class StoreCouponRestrictionsTests
{
    private static readonly Guid StoreId = new("A601BE8D-8D2D-4A79-B0E2-272490194669");

    [Fact]
    public async Task DisabledStoreDoesNotExposeCoupons()
    {
        var service = new CouponService(null!, NullLogger<CouponService>.Instance);
        Assert.Empty(await service.GetStoreCouponsAsync(StoreId));
    }

    [Fact]
    public async Task DisabledStoreCannotCreateCoupons()
    {
        var service = new CouponService(null!, NullLogger<CouponService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateCouponAsync(new CreateCouponDto
        {
            StoreId = StoreId,
            Code = "SAVE10"
        }));
    }
}
