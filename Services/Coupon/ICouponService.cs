using onlineStore.DTOs.Coupon;

namespace onlineStore.Services.Coupon
{
    public interface ICouponService
    {
        Task<List<CouponDto>> GetStoreCouponsAsync(Guid storeId);
        Task<CouponDto?> GetCouponByIdAsync(Guid id);
        Task<CouponDto> CreateCouponAsync(CreateCouponDto dto);
        Task<CouponDto?> UpdateCouponAsync(Guid id, UpdateCouponDto dto);
        Task<bool> DeleteCouponAsync(Guid id);
    }
}
