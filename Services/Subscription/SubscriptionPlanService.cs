using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Subscription;
using onlineStore.Models.Subscriptions;
using onlineStore.Security;

namespace onlineStore.Services.Subscription
{
    public class SubscriptionPlanService : ISubscriptionPlanService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SubscriptionPlanService> _logger;
        private readonly ICurrentUserService _currentUser;

        public SubscriptionPlanService(
            AppDbContext context,
            ILogger<SubscriptionPlanService> logger,
            ICurrentUserService currentUser)
        {
            _context = context;
            _logger = logger;
            _currentUser = currentUser;
        }

        public async Task<List<SubscriptionPlanDto>> GetAllAsync()
        {
            var plans = await _context.SubscriptionPlans
                .AsNoTracking()
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ToListAsync();

            return plans.Select(ToDto).ToList();
        }

        public async Task<SubscriptionPlanDto?> GetByIdAsync(Guid id)
        {
            var plan = await _context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            return plan == null ? null : ToDto(plan);
        }

        public async Task<SubscriptionPlanDto> CreateAsync(CreateSubscriptionPlanDto dto)
        {
            EnsureSuperAdmin();
            ValidateLimits(dto.MaxProducts, dto.MaxImagesPerProduct);

            var code = NormalizeCode(dto.Code);
            var exists = await _context.SubscriptionPlans
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(x => x.Code == code);

            if (exists)
                throw new InvalidOperationException("Subscription plan code already exists.");

            var plan = new SubscriptionPlan
            {
                Name = dto.Name.Trim(),
                Code = code,
                Description = NormalizeNullable(dto.Description),
                Price = dto.Price,
                Currency = NormalizeCurrency(dto.Currency),
                MaxProducts = dto.MaxProducts,
                MaxImagesPerProduct = dto.MaxImagesPerProduct,
                CanUseOffers = dto.CanUseOffers,
                CanUseCoupons = dto.CanUseCoupons,
                CanUseAnalytics = dto.CanUseAnalytics,
                CanUseAdvancedOffers = dto.CanUseAdvancedOffers,
                CanUseCustomDomain = dto.CanUseCustomDomain,
                IsActive = dto.IsActive,
                SortOrder = dto.SortOrder,
                CreatedAt = DateTime.UtcNow
            };

            _context.SubscriptionPlans.Add(plan);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Subscription plan created. PlanId: {PlanId}, Code: {Code}", plan.Id, plan.Code);

            return ToDto(plan);
        }

        public async Task<SubscriptionPlanDto?> UpdateAsync(Guid id, UpdateSubscriptionPlanDto dto)
        {
            EnsureSuperAdmin();

            var plan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(x => x.Id == id);

            if (plan == null)
                return null;

            var newMaxProducts = dto.MaxProducts ?? plan.MaxProducts;
            var newMaxImages = dto.MaxImagesPerProduct ?? plan.MaxImagesPerProduct;
            ValidateLimits(newMaxProducts, newMaxImages);

            if (dto.Code != null)
            {
                var newCode = NormalizeCode(dto.Code);

                var codeExists = await _context.SubscriptionPlans
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .AnyAsync(x => x.Code == newCode && x.Id != id);

                if (codeExists)
                    throw new InvalidOperationException("Subscription plan code already exists.");

                plan.Code = newCode;
            }

            if (dto.Name != null) plan.Name = dto.Name.Trim();
            if (dto.Description != null) plan.Description = NormalizeNullable(dto.Description);
            if (dto.Price.HasValue) plan.Price = dto.Price.Value;
            if (dto.Currency != null) plan.Currency = NormalizeCurrency(dto.Currency);
            if (dto.MaxProducts.HasValue) plan.MaxProducts = dto.MaxProducts;
            if (dto.MaxImagesPerProduct.HasValue) plan.MaxImagesPerProduct = dto.MaxImagesPerProduct;
            if (dto.CanUseOffers.HasValue) plan.CanUseOffers = dto.CanUseOffers.Value;
            if (dto.CanUseCoupons.HasValue) plan.CanUseCoupons = dto.CanUseCoupons.Value;
            if (dto.CanUseAnalytics.HasValue) plan.CanUseAnalytics = dto.CanUseAnalytics.Value;
            if (dto.CanUseAdvancedOffers.HasValue) plan.CanUseAdvancedOffers = dto.CanUseAdvancedOffers.Value;
            if (dto.CanUseCustomDomain.HasValue) plan.CanUseCustomDomain = dto.CanUseCustomDomain.Value;
            if (dto.IsActive.HasValue) plan.IsActive = dto.IsActive.Value;
            if (dto.SortOrder.HasValue) plan.SortOrder = dto.SortOrder.Value;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Subscription plan updated. PlanId: {PlanId}", plan.Id);

            return ToDto(plan);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            EnsureSuperAdmin();

            var plan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(x => x.Id == id);

            if (plan == null)
                return false;

            var hasActiveSubscriptions = await _context.StoreSubscriptions
                .AsNoTracking()
                .AnyAsync(x => x.SubscriptionPlanId == id && !x.IsDeleted);

            if (hasActiveSubscriptions)
                throw new InvalidOperationException("Cannot delete a plan that has subscriptions.");

            plan.IsDeleted = true;
            plan.IsActive = false;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Subscription plan soft deleted. PlanId: {PlanId}", plan.Id);

            return true;
        }

        public async Task EnsureDefaultPlansSeededAsync()
        {
            var basePlans = new List<SubscriptionPlan>
            {
                new()
                {
                    Name = "Free",
                    Code = SubscriptionPlanCodes.Free,
                    Description = "Starter plan with basic limits.",
                    Price = 0m,
                    Currency = "ILS",
                    MaxProducts = 20,
                    MaxImagesPerProduct = 2,
                    CanUseOffers = false,
                    CanUseCoupons = false,
                    CanUseAnalytics = false,
                    CanUseAdvancedOffers = false,
                    CanUseCustomDomain = false,
                    IsActive = true,
                    SortOrder = 1
                },
                new()
                {
                    Name = "Standard",
                    Code = SubscriptionPlanCodes.Standard,
                    Description = "Standard plan for growing stores.",
                    Price = 40m,
                    Currency = "ILS",
                    MaxProducts = 100,
                    MaxImagesPerProduct = 5,
                    CanUseOffers = true,
                    CanUseCoupons = true,
                    CanUseAnalytics = true,
                    CanUseAdvancedOffers = false,
                    CanUseCustomDomain = false,
                    IsActive = true,
                    SortOrder = 2
                },
                new()
                {
                    Name = "Pro",
                    Code = SubscriptionPlanCodes.Pro,
                    Description = "Top tier plan with unlimited capacity.",
                    Price = 70m,
                    Currency = "ILS",
                    MaxProducts = null,
                    MaxImagesPerProduct = null,
                    CanUseOffers = true,
                    CanUseCoupons = true,
                    CanUseAnalytics = true,
                    CanUseAdvancedOffers = true,
                    CanUseCustomDomain = true,
                    IsActive = true,
                    SortOrder = 3
                }
            };

            foreach (var basePlan in basePlans)
            {
                var exists = await _context.SubscriptionPlans
                    .IgnoreQueryFilters()
                    .AnyAsync(x => x.Code == basePlan.Code);

                if (exists)
                    continue;

                _context.SubscriptionPlans.Add(basePlan);
            }

            await _context.SaveChangesAsync();
        }

        private void EnsureSuperAdmin()
        {
            if (!_currentUser.IsSuperAdmin)
                throw new UnauthorizedAccessException("Only super admins can manage subscription plans.");
        }

        private static string NormalizeCode(string code)
        {
            return code.Trim().ToUpperInvariant();
        }

        private static string NormalizeCurrency(string currency)
        {
            return currency.Trim().ToUpperInvariant();
        }

        private static string? NormalizeNullable(string? value)
        {
            if (value == null)
                return null;

            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static void ValidateLimits(int? maxProducts, int? maxImagesPerProduct)
        {
            if (maxProducts.HasValue && maxProducts.Value < 0)
                throw new InvalidOperationException("MaxProducts cannot be less than 0.");

            if (maxImagesPerProduct.HasValue && maxImagesPerProduct.Value < 0)
                throw new InvalidOperationException("MaxImagesPerProduct cannot be less than 0.");
        }

        private static SubscriptionPlanDto ToDto(SubscriptionPlan plan)
        {
            return new SubscriptionPlanDto
            {
                Id = plan.Id,
                Name = plan.Name,
                Code = plan.Code,
                Description = plan.Description,
                Price = plan.Price,
                Currency = plan.Currency,
                MaxProducts = plan.MaxProducts,
                MaxImagesPerProduct = plan.MaxImagesPerProduct,
                CanUseOffers = plan.CanUseOffers,
                CanUseCoupons = plan.CanUseCoupons,
                CanUseAnalytics = plan.CanUseAnalytics,
                CanUseAdvancedOffers = plan.CanUseAdvancedOffers,
                CanUseCustomDomain = plan.CanUseCustomDomain,
                IsActive = plan.IsActive,
                SortOrder = plan.SortOrder,
                CreatedAt = plan.CreatedAt,
                UpdatedAt = plan.UpdatedAt
            };
        }
    }
}
