using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Subscription;
using onlineStore.Models.Subscriptions;
using onlineStore.Models.Subscriptions.Enums;
using onlineStore.Security;

namespace onlineStore.Services.Subscription
{
    public class StoreSubscriptionService : IStoreSubscriptionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StoreSubscriptionService> _logger;
        private readonly ICurrentUserService _currentUser;
        private readonly IStoreOwnershipService _storeOwnershipService;

        public StoreSubscriptionService(
            AppDbContext context,
            ILogger<StoreSubscriptionService> logger,
            ICurrentUserService currentUser,
            IStoreOwnershipService storeOwnershipService)
        {
            _context = context;
            _logger = logger;
            _currentUser = currentUser;
            _storeOwnershipService = storeOwnershipService;
        }

        public async Task<List<StoreSubscriptionDto>> GetStoreSubscriptionsAsync(Guid storeId)
        {
            await EnsureCanReadStoreAsync(storeId);

            var subscriptions = await _context.StoreSubscriptions
                .AsNoTracking()
                .Include(x => x.SubscriptionPlan)
                .Where(x => x.StoreId == storeId)
                .OrderByDescending(x => x.StartDate)
                .ToListAsync();

            return subscriptions.Select(ToDto).ToList();
        }

        public async Task<StoreSubscriptionDto?> GetActiveSubscriptionForStoreAsync(Guid storeId)
        {
            await EnsureCanReadStoreAsync(storeId);
            return await GetActiveSubscriptionAsync(storeId);
        }

        public async Task<StoreSubscriptionDto?> GetActiveSubscriptionAsync(Guid storeId)
        {
            var active = await GetActiveSubscriptionEntityAsync(storeId, autoAssignDefaultPlan: true);
            return active == null ? null : ToDto(active);
        }

        public async Task<StoreSubscriptionDto> AssignStoreSubscriptionAsync(AssignStoreSubscriptionDto dto)
        {
            EnsureSuperAdmin();

            if (dto.StoreId == Guid.Empty)
                throw new InvalidOperationException("StoreId is required.");

            if (dto.EndDate.HasValue && dto.StartDate.HasValue && dto.EndDate.Value < dto.StartDate.Value)
                throw new InvalidOperationException("EndDate cannot be before StartDate.");

            var storeExists = await _context.Stores
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(s => s.Id == dto.StoreId && !s.IsDeleted);

            if (!storeExists)
                throw new KeyNotFoundException("Store not found.");

            var plan = await _context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == dto.SubscriptionPlanId && x.IsActive);

            if (plan == null)
                throw new InvalidOperationException("Subscription plan not found or inactive.");

            var startDate = dto.StartDate?.ToUniversalTime() ?? DateTime.UtcNow;

            var activeSubscriptions = await _context.StoreSubscriptions
                .Where(x => x.StoreId == dto.StoreId &&
                            x.Status == SubscriptionStatus.Active &&
                            !x.IsDeleted &&
                            (!x.EndDate.HasValue || x.EndDate >= startDate))
                .ToListAsync();

            foreach (var current in activeSubscriptions)
            {
                current.Status = SubscriptionStatus.Cancelled;
                current.EndDate = startDate.AddSeconds(-1);
            }

            var subscription = new StoreSubscription
            {
                StoreId = dto.StoreId,
                SubscriptionPlanId = dto.SubscriptionPlanId,
                StartDate = startDate,
                EndDate = dto.EndDate?.ToUniversalTime(),
                Status = SubscriptionStatus.Active,
                PaidAmount = dto.PaidAmount,
                Currency = NormalizeCurrency(dto.Currency),
                IsAutoRenew = dto.IsAutoRenew,
                Notes = NormalizeNullable(dto.Notes),
                CreatedAt = DateTime.UtcNow
            };

            _context.StoreSubscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            var created = await _context.StoreSubscriptions
                .AsNoTracking()
                .Include(x => x.SubscriptionPlan)
                .FirstAsync(x => x.Id == subscription.Id);

            _logger.LogInformation(
                "Store subscription assigned. StoreId: {StoreId}, SubscriptionId: {SubscriptionId}, PlanCode: {PlanCode}",
                subscription.StoreId,
                subscription.Id,
                created.SubscriptionPlan.Code);

            return ToDto(created);
        }

        public async Task<StoreSubscriptionDto?> ChangeStoreSubscriptionAsync(Guid storeSubscriptionId, ChangeStoreSubscriptionDto dto)
        {
            EnsureSuperAdmin();

            var current = await _context.StoreSubscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == storeSubscriptionId);

            if (current == null)
                return null;

            var assigned = await AssignStoreSubscriptionAsync(new AssignStoreSubscriptionDto
            {
                StoreId = current.StoreId,
                SubscriptionPlanId = dto.NewSubscriptionPlanId,
                StartDate = dto.EffectiveFrom,
                PaidAmount = dto.PaidAmount,
                Currency = dto.Currency,
                IsAutoRenew = dto.IsAutoRenew,
                Notes = dto.Notes
            });

            return assigned;
        }

        public async Task<bool> CancelStoreSubscriptionAsync(Guid storeSubscriptionId, string? notes = null)
        {
            EnsureSuperAdmin();

            var subscription = await _context.StoreSubscriptions
                .FirstOrDefaultAsync(x => x.Id == storeSubscriptionId);

            if (subscription == null)
                return false;

            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.EndDate ??= DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(notes))
            {
                var normalized = notes.Trim();
                subscription.Notes = string.IsNullOrWhiteSpace(subscription.Notes)
                    ? normalized
                    : $"{subscription.Notes} | {normalized}";
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Store subscription cancelled. SubscriptionId: {SubscriptionId}, StoreId: {StoreId}",
                subscription.Id,
                subscription.StoreId);

            return true;
        }

        public async Task<StorePlanLimitsDto> GetStorePlanLimitsAsync(Guid storeId)
        {
            var active = await GetActiveSubscriptionEntityAsync(storeId, autoAssignDefaultPlan: true)
                ?? throw new InvalidOperationException("No active subscription is configured for the store.");

            return new StorePlanLimitsDto
            {
                StoreId = storeId,
                SubscriptionPlanId = active.SubscriptionPlanId,
                PlanName = active.SubscriptionPlan.Name,
                PlanCode = active.SubscriptionPlan.Code,
                MaxProducts = active.SubscriptionPlan.MaxProducts,
                MaxImagesPerProduct = active.SubscriptionPlan.MaxImagesPerProduct,
                CanUseOffers = active.SubscriptionPlan.CanUseOffers
            };
        }

        public async Task<bool> CanStoreCreateProductAsync(Guid storeId)
        {
            var limits = await GetStorePlanLimitsAsync(storeId);

            if (!limits.MaxProducts.HasValue)
                return true;

            var currentProductCount = await _context.Products
                .AsNoTracking()
                .CountAsync(p => p.StoreId == storeId);

            return currentProductCount < limits.MaxProducts.Value;
        }

        public async Task<bool> CanStoreAddProductImageAsync(Guid productId)
        {
            var productInfo = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => new { p.Id, p.StoreId })
                .FirstOrDefaultAsync();

            if (productInfo == null)
                throw new KeyNotFoundException("Product not found.");

            var limits = await GetStorePlanLimitsAsync(productInfo.StoreId);

            if (!limits.MaxImagesPerProduct.HasValue)
                return true;

            var currentImageCount = await _context.ProductImages
                .AsNoTracking()
                .CountAsync(i => i.ProductId == productId);

            return currentImageCount < limits.MaxImagesPerProduct.Value;
        }

        public async Task<bool> CanStoreUseOffersAsync(Guid storeId)
        {
            var limits = await GetStorePlanLimitsAsync(storeId);
            return limits.CanUseOffers;
        }

        private async Task<StoreSubscription?> GetActiveSubscriptionEntityAsync(Guid storeId, bool autoAssignDefaultPlan)
        {
            var now = DateTime.UtcNow;

            var active = await _context.StoreSubscriptions
                .Include(x => x.SubscriptionPlan)
                .OrderByDescending(x => x.StartDate)
                .FirstOrDefaultAsync(x =>
                    x.StoreId == storeId &&
                    x.Status == SubscriptionStatus.Active &&
                    !x.IsDeleted &&
                    x.StartDate <= now &&
                    (!x.EndDate.HasValue || x.EndDate >= now) &&
                    x.SubscriptionPlan.IsActive &&
                    !x.SubscriptionPlan.IsDeleted);

            if (active != null)
                return active;

            if (!autoAssignDefaultPlan)
                return null;

            var defaultPlan = await _context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == SubscriptionPlanCodes.DefaultStorePlan && x.IsActive);

            if (defaultPlan == null)
                throw new InvalidOperationException($"Default {SubscriptionPlanCodes.DefaultStorePlan} subscription plan is not configured.");

            var exists = await _context.Stores
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(s => s.Id == storeId && !s.IsDeleted);

            if (!exists)
                throw new KeyNotFoundException("Store not found.");

            var autoSubscription = new StoreSubscription
            {
                StoreId = storeId,
                SubscriptionPlanId = defaultPlan.Id,
                StartDate = DateTime.UtcNow,
                Status = SubscriptionStatus.Active,
                PaidAmount = defaultPlan.Price,
                Currency = defaultPlan.Currency,
                IsAutoRenew = true,
                Notes = $"Auto-assigned default {SubscriptionPlanCodes.DefaultStorePlan} plan",
                CreatedAt = DateTime.UtcNow
            };

            _context.StoreSubscriptions.Add(autoSubscription);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Default {PlanCode} plan auto-assigned. StoreId: {StoreId}, SubscriptionId: {SubscriptionId}",
                storeId,
                autoSubscription.Id,
                SubscriptionPlanCodes.DefaultStorePlan);

            return await _context.StoreSubscriptions
                .Include(x => x.SubscriptionPlan)
                .FirstAsync(x => x.Id == autoSubscription.Id);
        }

        private async Task EnsureCanReadStoreAsync(Guid storeId)
        {
            if (_currentUser.IsSuperAdmin)
                return;

            if (!_currentUser.IsStoreOwner || !_currentUser.UserId.HasValue)
                throw new UnauthorizedAccessException("You are not authorized to access this store.");

            var ownsStore = await _storeOwnershipService.UserOwnsStoreAsync(storeId, _currentUser.UserId.Value);
            if (!ownsStore)
                throw new KeyNotFoundException("Store not found.");
        }

        private void EnsureSuperAdmin()
        {
            if (!_currentUser.IsSuperAdmin)
                throw new UnauthorizedAccessException("Only super admins can manage store subscriptions.");
        }

        private static string NormalizeCurrency(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "USD" : value.Trim().ToUpperInvariant();
        }

        private static string? NormalizeNullable(string? value)
        {
            if (value == null)
                return null;

            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static StoreSubscriptionDto ToDto(StoreSubscription subscription)
        {
            return new StoreSubscriptionDto
            {
                Id = subscription.Id,
                StoreId = subscription.StoreId,
                SubscriptionPlanId = subscription.SubscriptionPlanId,
                PlanName = subscription.SubscriptionPlan.Name,
                PlanCode = subscription.SubscriptionPlan.Code,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                Status = subscription.Status,
                PaidAmount = subscription.PaidAmount,
                Currency = subscription.Currency,
                IsAutoRenew = subscription.IsAutoRenew,
                Notes = subscription.Notes,
                CreatedAt = subscription.CreatedAt,
                UpdatedAt = subscription.UpdatedAt
            };
        }
    }
}
