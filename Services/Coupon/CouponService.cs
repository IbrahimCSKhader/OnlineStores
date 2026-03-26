using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Coupon;
using onlineStore.Models.Enums;
using CouponEntity = onlineStore.Models.Discounts.Coupon;

namespace onlineStore.Services.Coupon
{
    public class CouponService : ICouponService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CouponService> _logger;

        public CouponService(
            AppDbContext context,
            ILogger<CouponService> logger)
        {
            _context = context;
            _logger = logger;
        }


        public async Task<List<CouponDto>> GetStoreCouponsAsync(Guid storeId)
        {
            return await _context.Coupons
                .AsNoTracking()
                .Where(c => c.StoreId == storeId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => ToDto(c))
                .ToListAsync();
        }


        public async Task<CouponDto?> GetCouponByIdAsync(Guid id)
        {
            var coupon = await _context.Coupons
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            return coupon == null ? null : ToDto(coupon);
        }

         public async Task<CouponDto> CreateCouponAsync(CreateCouponDto dto)
        {
            await ValidateCreateOrUpdateAsync(dto.Code, dto.DiscountType, dto.DiscountValue,
                dto.MinOrderAmount, dto.MaxDiscountAmount, dto.StartsAt, dto.ExpiresAt,
                dto.UsageLimit, dto.PerUserLimit, dto.StoreId, null);

            var normalizedCode = NormalizeCode(dto.Code);

            var coupon = new CouponEntity
            {
                Code = normalizedCode,
                Description = dto.Description?.Trim(),
                DiscountType = dto.DiscountType,
                DiscountValue = dto.DiscountValue,
                MinOrderAmount = dto.MinOrderAmount,
                MaxDiscountAmount = dto.MaxDiscountAmount,
                UsageLimit = dto.UsageLimit,
                UsageCount = 0,
                PerUserLimit = dto.PerUserLimit,
                StartsAt = dto.StartsAt,
                ExpiresAt = dto.ExpiresAt,
                IsActive = dto.IsActive,
                StoreId = dto.StoreId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Coupons.Add(coupon);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Coupon created: {CouponCode} for store {StoreId}",
                coupon.Code, coupon.StoreId);

            return ToDto(coupon);
        }


        public async Task<CouponDto?> UpdateCouponAsync(Guid id, UpdateCouponDto dto)
        {
            var coupon = await _context.Coupons
                .FirstOrDefaultAsync(c => c.Id == id);

            if (coupon == null)
                return null;

            var newCode = dto.Code != null ? NormalizeCode(dto.Code) : coupon.Code;
            var newDiscountType = dto.DiscountType ?? coupon.DiscountType;
            var newDiscountValue = dto.DiscountValue ?? coupon.DiscountValue;
            var newMinOrderAmount = dto.MinOrderAmount ?? coupon.MinOrderAmount;
            var newMaxDiscountAmount = dto.MaxDiscountAmount ?? coupon.MaxDiscountAmount;
            var newStartsAt = dto.StartsAt ?? coupon.StartsAt;
            var newExpiresAt = dto.ExpiresAt ?? coupon.ExpiresAt;
            var newUsageLimit = dto.UsageLimit ?? coupon.UsageLimit;
            var newPerUserLimit = dto.PerUserLimit ?? coupon.PerUserLimit;

            await ValidateCreateOrUpdateAsync(
                newCode,
                newDiscountType,
                newDiscountValue,
                newMinOrderAmount,
                newMaxDiscountAmount,
                newStartsAt,
                newExpiresAt,
                newUsageLimit,
                newPerUserLimit,
                coupon.StoreId,
                coupon.Id);

            if (dto.Code != null) coupon.Code = newCode;
            if (dto.Description != null) coupon.Description = dto.Description.Trim();
            if (dto.DiscountType.HasValue) coupon.DiscountType = dto.DiscountType.Value;
            if (dto.DiscountValue.HasValue) coupon.DiscountValue = dto.DiscountValue.Value;
            if (dto.MinOrderAmount.HasValue) coupon.MinOrderAmount = dto.MinOrderAmount.Value;
            if (dto.MaxDiscountAmount.HasValue) coupon.MaxDiscountAmount = dto.MaxDiscountAmount.Value;
            if (dto.UsageLimit.HasValue) coupon.UsageLimit = dto.UsageLimit.Value;
            if (dto.PerUserLimit.HasValue) coupon.PerUserLimit = dto.PerUserLimit.Value;
            if (dto.StartsAt.HasValue) coupon.StartsAt = dto.StartsAt.Value;
            if (dto.ExpiresAt.HasValue) coupon.ExpiresAt = dto.ExpiresAt.Value;
            if (dto.IsActive.HasValue) coupon.IsActive = dto.IsActive.Value;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Coupon updated: {CouponId}", coupon.Id);

            return ToDto(coupon);
        }

        public async Task<bool> DeleteCouponAsync(Guid id)
        {
            var coupon = await _context.Coupons
                .FirstOrDefaultAsync(c => c.Id == id);

            if (coupon == null)
                return false;

            coupon.IsDeleted = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Coupon deleted: {CouponId}", id);

            return true;
        }


        private async Task ValidateCreateOrUpdateAsync(
            string code,
            DiscountType discountType,
            decimal discountValue,
            decimal? minOrderAmount,
            decimal? maxDiscountAmount,
            DateTime? startsAt,
            DateTime? expiresAt,
            int? usageLimit,
            int? perUserLimit,
            Guid storeId,
            Guid? currentCouponId)
        {
            if (storeId == Guid.Empty)
                throw new Exception("unvalid store id");

            var storeExists = await _context.Stores
                .AsNoTracking()
                .AnyAsync(s => s.Id == storeId);

            if (!storeExists)
                throw new Exception("store not exist");

            var normalizedCode = NormalizeCode(code);

            var codeExists = await _context.Coupons
                .IgnoreQueryFilters()
                .AnyAsync(c => c.Code == normalizedCode && c.Id != currentCouponId);

            if (codeExists)
                throw new Exception("copun id is already used");

            if (discountValue <= 0)
                throw new Exception("copun value must be more than 0");

            if (discountType == DiscountType.Percentage && discountValue > 100)
                throw new Exception("percantage discount can not be more than 100");

            if (minOrderAmount.HasValue && minOrderAmount.Value < 0)
                throw new Exception("min value for discount didnt valid");

            if (maxDiscountAmount.HasValue && maxDiscountAmount.Value < 0)
                throw new Exception("max value for discount didnt valid");

            if (startsAt.HasValue && expiresAt.HasValue && startsAt.Value > expiresAt.Value)
                throw new Exception("start date must be before end date ");

            if (usageLimit.HasValue && usageLimit.Value <= 0)
                throw new Exception("used number must be more than 0");

            if (perUserLimit.HasValue && perUserLimit.Value <= 0)
                throw new Exception("number of uses must be more the 0 for user");
        }


        private static string NormalizeCode(string code)
        {
            return code.Trim().ToUpperInvariant();
        }

        private static CouponDto ToDto(CouponEntity c) => new()
        {
            Id = c.Id,
            Code = c.Code,
            Description = c.Description,
            DiscountType = c.DiscountType,
            DiscountValue = c.DiscountValue,
            MinOrderAmount = c.MinOrderAmount,
            MaxDiscountAmount = c.MaxDiscountAmount,
            UsageLimit = c.UsageLimit,
            UsageCount = c.UsageCount,
            PerUserLimit = c.PerUserLimit,
            StartsAt = c.StartsAt,
            ExpiresAt = c.ExpiresAt,
            IsActive = c.IsActive,
            StoreId = c.StoreId,
            CreatedAt = c.CreatedAt
        };
    }
}