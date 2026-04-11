using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Subscription;
using onlineStore.Models.Subscriptions;
using onlineStore.Models.Subscriptions.Enums;
using onlineStore.Security;

namespace onlineStore.Services.Subscription
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SubscriptionService> _logger;
        private readonly ICurrentUserService _currentUser;
        private readonly IStoreOwnershipService _storeOwnershipService;

        public SubscriptionService(
            AppDbContext context,
            ILogger<SubscriptionService> logger,
            ICurrentUserService currentUser,
            IStoreOwnershipService storeOwnershipService)
        {
            _context = context;
            _logger = logger;
            _currentUser = currentUser;
            _storeOwnershipService = storeOwnershipService;
        }

        public async Task<List<SubscriptionPlanDto>> GetAllPlansAsync()
        {
            var plans = await _context.SubscriptionPlans
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Price)
                .ToListAsync();

            return plans.Select(ToPlanDto).ToList();
        }

        public async Task<SubscriptionPlanDto?> GetPlanByIdAsync(Guid id)
        {
            var plan = await _context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            return plan == null ? null : ToPlanDto(plan);
        }

        public async Task<StoreSubscriptionDto?> GetActiveSubscriptionAsync(Guid storeId)
        {
            await EnsureCanManageStoreAsync(storeId);
            var subscription = await GetOrAssignDefaultSubscriptionEntityAsync(storeId);
            return subscription == null ? null : ToStoreSubscriptionDto(subscription);
        }

        public async Task<StoreSubscriptionDto> AssignPlanToStoreAsync(Guid storeId, Guid planId)
        {
            await EnsureCanManageStoreAsync(storeId);
            return await AssignPlanInternalAsync(storeId, planId);
        }

        public async Task<StoreSubscriptionDto> ChangePlanAsync(Guid storeId, Guid newPlanId)
        {
            await EnsureCanManageStoreAsync(storeId);
            return await AssignPlanInternalAsync(storeId, newPlanId);
        }

        public async Task<bool> CanStoreCreateProductAsync(Guid storeId)
        {
            var plan = await GetPlanForStoreAsync(storeId);
            if (!plan.MaxProducts.HasValue)
                return true;

            var productsCount = await _context.Products
                .AsNoTracking()
                .CountAsync(p => p.StoreId == storeId);

            return productsCount < plan.MaxProducts.Value;
        }

        public async Task<bool> CanStoreAddImageAsync(Guid productId)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => new { p.Id, p.StoreId })
                .FirstOrDefaultAsync();

            if (product == null)
                throw new KeyNotFoundException("المنتج غير موجود");

            var plan = await GetPlanForStoreAsync(product.StoreId);
            if (!plan.MaxImagesPerProduct.HasValue)
                return true;

            var imagesCount = await _context.ProductImages
                .AsNoTracking()
                .CountAsync(i => i.ProductId == productId);

            return imagesCount < plan.MaxImagesPerProduct.Value;
        }

        public async Task<bool> CanStoreUseOffersAsync(Guid storeId)
        {
            var plan = await GetPlanForStoreAsync(storeId);
            return plan.CanUseOffers;
        }

        public async Task EnsureDefaultPlansSeededAsync()
        {
            var plans = new List<SubscriptionPlan>
            {
                new()
                {
                    Name = "Free",
                    Code = SubscriptionPlanCodes.Free,
                    Price = 0m,
                    MaxProducts = 20,
                    MaxImagesPerProduct = 2,
                    CanUseOffers = false,
                    CanUseCoupons = false,
                    CanUseAnalytics = false,
                    CanUseAdvancedOffers = false,
                    CanUseCustomDomain = false,
                    IsActive = true,
                    SortOrder = 1,
                    Currency = "ILS"
                },
                new()
                {
                    Name = "Standard",
                    Code = SubscriptionPlanCodes.Standard,
                    Price = 40m,
                    MaxProducts = 100,
                    MaxImagesPerProduct = 5,
                    CanUseOffers = true,
                    CanUseCoupons = true,
                    CanUseAnalytics = true,
                    CanUseAdvancedOffers = false,
                    CanUseCustomDomain = false,
                    IsActive = true,
                    SortOrder = 2,
                    Currency = "ILS"
                },
                new()
                {
                    Name = "Pro",
                    Code = SubscriptionPlanCodes.Pro,
                    Price = 70m,
                    MaxProducts = null,
                    MaxImagesPerProduct = null,
                    CanUseOffers = true,
                    CanUseCoupons = true,
                    CanUseAnalytics = true,
                    CanUseAdvancedOffers = true,
                    CanUseCustomDomain = true,
                    IsActive = true,
                    SortOrder = 3,
                    Currency = "ILS"
                }
            };

            foreach (var plan in plans)
            {
                var existing = await _context.SubscriptionPlans
                    .FirstOrDefaultAsync(x => x.Code == plan.Code);

                if (existing == null)
                {
                    _context.SubscriptionPlans.Add(plan);
                    continue;
                }

                existing.Name = plan.Name;
                existing.Price = plan.Price;
                existing.MaxProducts = plan.MaxProducts;
                existing.MaxImagesPerProduct = plan.MaxImagesPerProduct;
                existing.CanUseOffers = plan.CanUseOffers;
                existing.CanUseCoupons = plan.CanUseCoupons;
                existing.CanUseAnalytics = plan.CanUseAnalytics;
                existing.CanUseAdvancedOffers = plan.CanUseAdvancedOffers;
                existing.CanUseCustomDomain = plan.CanUseCustomDomain;
                existing.IsActive = true;
                existing.SortOrder = plan.SortOrder;
                existing.Currency = plan.Currency;
            }

            await _context.SaveChangesAsync();
            await EnsureAllStoresUseDefaultPlanAsync();
        }

        private async Task<StoreSubscriptionDto> AssignPlanInternalAsync(Guid storeId, Guid planId)
        {
            var storeExists = await _context.Stores
                .AsNoTracking()
                .AnyAsync(s => s.Id == storeId);

            if (!storeExists)
                throw new KeyNotFoundException("المتجر غير موجود");

            var plan = await _context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive);

            if (plan == null)
                throw new InvalidOperationException("الباقة غير موجودة أو غير مفعلة");

            var now = DateTime.UtcNow;

            var activeSubscriptions = await _context.StoreSubscriptions
                .Where(s => s.StoreId == storeId && s.Status == SubscriptionStatus.Active)
                .ToListAsync();

            foreach (var active in activeSubscriptions)
            {
                active.Status = SubscriptionStatus.Expired;
                active.EndDate = now;
            }

            var subscription = new StoreSubscription
            {
                StoreId = storeId,
                SubscriptionPlanId = planId,
                StartDate = now,
                Status = SubscriptionStatus.Active,
                PaidAmount = plan.Price,
                Currency = plan.Currency,
                IsAutoRenew = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.StoreSubscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            var created = await _context.StoreSubscriptions
                .AsNoTracking()
                .Include(s => s.SubscriptionPlan)
                .FirstAsync(s => s.Id == subscription.Id);

            _logger.LogInformation("Subscription assigned. StoreId: {StoreId}, Plan: {PlanCode}", storeId, created.SubscriptionPlan.Code);

            return ToStoreSubscriptionDto(created);
        }

        private async Task<StoreSubscription?> GetOrAssignDefaultSubscriptionEntityAsync(Guid storeId)
        {
            var now = DateTime.UtcNow;
            var active = await _context.StoreSubscriptions
                .AsNoTracking()
                .Include(s => s.SubscriptionPlan)
                .Where(s => s.StoreId == storeId && s.Status == SubscriptionStatus.Active && (!s.EndDate.HasValue || s.EndDate >= now))
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync();

            if (active != null)
                return active;

            var defaultPlan = await _context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code == SubscriptionPlanCodes.DefaultStorePlan && p.IsActive);

            if (defaultPlan == null)
                throw new InvalidOperationException($"{SubscriptionPlanCodes.DefaultStorePlan} plan is not configured");

            await AssignPlanInternalAsync(storeId, defaultPlan.Id);

            return await _context.StoreSubscriptions
                .AsNoTracking()
                .Include(s => s.SubscriptionPlan)
                .Where(s => s.StoreId == storeId && s.Status == SubscriptionStatus.Active)
                .OrderByDescending(s => s.StartDate)
                .FirstAsync();
        }

        private async Task<SubscriptionPlan> GetPlanForStoreAsync(Guid storeId)
        {
            var subscription = await GetOrAssignDefaultSubscriptionEntityAsync(storeId)
                ?? throw new InvalidOperationException("لا يوجد اشتراك فعال لهذا المتجر");

            return subscription.SubscriptionPlan;
        }

        private async Task EnsureAllStoresUseDefaultPlanAsync()
        {
            var defaultPlan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.Code == SubscriptionPlanCodes.DefaultStorePlan && p.IsActive);

            if (defaultPlan == null)
                throw new InvalidOperationException($"{SubscriptionPlanCodes.DefaultStorePlan} plan is not configured");

            var now = DateTime.UtcNow;
            var storeIds = await _context.Stores
                .AsNoTracking()
                .Select(s => s.Id)
                .ToListAsync();

            if (storeIds.Count == 0)
                return;

            var activeSubscriptions = await _context.StoreSubscriptions
                .Where(s => storeIds.Contains(s.StoreId)
                    && s.Status == SubscriptionStatus.Active
                    && (!s.EndDate.HasValue || s.EndDate >= now))
                .OrderByDescending(s => s.StartDate)
                .ToListAsync();

            var latestActiveByStore = activeSubscriptions
                .GroupBy(s => s.StoreId)
                .ToDictionary(g => g.Key, g => g.First());

            var storesNeedingDefaultPlan = storeIds
                .Where(storeId =>
                    !latestActiveByStore.TryGetValue(storeId, out var subscription)
                    || subscription.SubscriptionPlanId != defaultPlan.Id)
                .ToList();

            if (storesNeedingDefaultPlan.Count == 0)
                return;

            foreach (var activeSubscription in activeSubscriptions
                         .Where(s => storesNeedingDefaultPlan.Contains(s.StoreId)))
            {
                activeSubscription.Status = SubscriptionStatus.Expired;
                activeSubscription.EndDate = now;
            }

            foreach (var storeId in storesNeedingDefaultPlan)
            {
                _context.StoreSubscriptions.Add(new StoreSubscription
                {
                    StoreId = storeId,
                    SubscriptionPlanId = defaultPlan.Id,
                    StartDate = now,
                    Status = SubscriptionStatus.Active,
                    PaidAmount = defaultPlan.Price,
                    Currency = defaultPlan.Currency,
                    IsAutoRenew = true,
                    Notes = $"Auto-assigned default {SubscriptionPlanCodes.DefaultStorePlan} plan",
                    CreatedAt = now
                });
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Default plan {PlanCode} ensured for {StoreCount} stores.",
                SubscriptionPlanCodes.DefaultStorePlan,
                storesNeedingDefaultPlan.Count);
        }

        private async Task EnsureCanManageStoreAsync(Guid storeId)
        {
            if (_currentUser.IsSuperAdmin)
                return;

            if (!_currentUser.IsStoreOwner || !_currentUser.UserId.HasValue)
                throw new UnauthorizedAccessException("غير مصرح لك بالوصول لهذا المتجر");

            var ownsStore = await _storeOwnershipService.UserOwnsStoreAsync(storeId, _currentUser.UserId.Value);
            if (!ownsStore)
                throw new KeyNotFoundException("المتجر غير موجود");
        }

        private static SubscriptionPlanDto ToPlanDto(SubscriptionPlan plan)
        {
            return new SubscriptionPlanDto
            {
                Id = plan.Id,
                Name = plan.Name,
                Code = plan.Code,
                Price = plan.Price,
                MaxProducts = plan.MaxProducts,
                MaxImagesPerProduct = plan.MaxImagesPerProduct,
                CanUseOffers = plan.CanUseOffers,
                CanUseCoupons = plan.CanUseCoupons,
                CanUseAnalytics = plan.CanUseAnalytics,
                CanUseAdvancedOffers = plan.CanUseAdvancedOffers,
                CanUseCustomDomain = plan.CanUseCustomDomain,
                IsActive = plan.IsActive,
                CreatedAt = plan.CreatedAt
            };
        }

        private static StoreSubscriptionDto ToStoreSubscriptionDto(StoreSubscription subscription)
        {
            return new StoreSubscriptionDto
            {
                Id = subscription.Id,
                StoreId = subscription.StoreId,
                SubscriptionPlanId = subscription.SubscriptionPlanId,
                PlanName = subscription.SubscriptionPlan.Name,
                PlanCode = subscription.SubscriptionPlan.Code,
                MaxProducts = subscription.SubscriptionPlan.MaxProducts,
                MaxImagesPerProduct = subscription.SubscriptionPlan.MaxImagesPerProduct,
                CanUseOffers = subscription.SubscriptionPlan.CanUseOffers,
                CanUseCoupons = subscription.SubscriptionPlan.CanUseCoupons,
                CanUseAnalytics = subscription.SubscriptionPlan.CanUseAnalytics,
                CanUseAdvancedOffers = subscription.SubscriptionPlan.CanUseAdvancedOffers,
                CanUseCustomDomain = subscription.SubscriptionPlan.CanUseCustomDomain,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                Status = subscription.Status,
                CreatedAt = subscription.CreatedAt
            };
        }
    }
}
