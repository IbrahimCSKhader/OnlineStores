using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Offer;
using onlineStore.Models.Offers;
using onlineStore.Models.Offers.Enums;
using onlineStore.Security;
using onlineStore.Services.Subscription;

namespace onlineStore.Services.Offer
{
    public class OfferService : IOfferService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OfferService> _logger;
        private readonly ICurrentUserService _currentUser;
        private readonly IStoreOwnershipService _storeOwnershipService;
        private readonly ISubscriptionService _subscriptionService;

        public OfferService(
            AppDbContext context,
            ILogger<OfferService> logger,
            ICurrentUserService currentUser,
            IStoreOwnershipService storeOwnershipService,
            ISubscriptionService subscriptionService)
        {
            _context = context;
            _logger = logger;
            _currentUser = currentUser;
            _storeOwnershipService = storeOwnershipService;
            _subscriptionService = subscriptionService;
        }

        public async Task<List<OfferDto>> GetStoreOffersAsync(Guid storeId)
        {
            var canManageStore = await CanCurrentUserManageStoreAsync(storeId);
            var now = DateTime.UtcNow;

            IQueryable<Models.Offers.Offer> query = _context.Offers
                .AsNoTracking()
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Where(o => o.StoreId == storeId);

            if (!canManageStore)
            {
                query = query.Where(o => o.IsActive
                    && o.StartDate <= now
                    && (!o.EndDate.HasValue || o.EndDate >= now));
            }

            var offers = await query
                .OrderByDescending(o => o.Priority)
                .ThenByDescending(o => o.CreatedAt)
                .ToListAsync();

            return offers.Select(ToDto).ToList();
        }

        public async Task<List<OfferDto>> GetActiveOffersForStoreAsync(Guid storeId)
        {
            var now = DateTime.UtcNow;

            var offers = await _context.Offers
                .AsNoTracking()
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Where(o => o.StoreId == storeId
                            && o.IsActive
                            && o.StartDate <= now
                            && (!o.EndDate.HasValue || o.EndDate >= now))
                .OrderByDescending(o => o.Priority)
                .ThenByDescending(o => o.CreatedAt)
                .ToListAsync();

            return offers.Select(ToDto).ToList();
        }

        public async Task<OfferDto?> GetOfferByIdAsync(Guid id)
        {
            var offer = await _context.Offers
                .AsNoTracking()
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offer == null)
                return null;

            var canManageStore = await CanCurrentUserManageStoreAsync(offer.StoreId);
            if (!canManageStore)
            {
                var now = DateTime.UtcNow;
                var isPubliclyVisible = offer.IsActive
                    && offer.StartDate <= now
                    && (!offer.EndDate.HasValue || offer.EndDate >= now);

                if (!isPubliclyVisible)
                    return null;
            }

            return ToDto(offer);
        }

        public async Task<OfferDto> CreateOfferAsync(CreateOfferDto dto)
        {
            await EnsureCanManageStoreAsync(dto.StoreId);
            await ValidateOfferUsageAsync(dto.StoreId);
            ValidateOfferInput(dto.Type, dto.BundlePrice, dto.DiscountPercentage, dto.DiscountAmount, dto.StartDate, dto.EndDate);

            if (dto.Items == null || dto.Items.Count == 0)
                throw new InvalidOperationException("Offer must include at least one product.");

            await ValidateOfferItemsBelongToStoreAsync(dto.StoreId, dto.Items);

            var offer = new Models.Offers.Offer
            {
                StoreId = dto.StoreId,
                Name = dto.Name.Trim(),
                Description = NormalizeNullable(dto.Description),
                Type = dto.Type,
                IsActive = dto.IsActive,
                StartDate = dto.StartDate.ToUniversalTime(),
                EndDate = dto.EndDate?.ToUniversalTime(),
                Priority = dto.Priority,
                BundlePrice = dto.BundlePrice,
                DiscountPercentage = dto.DiscountPercentage,
                DiscountAmount = dto.DiscountAmount,
                AppliesToAllProducts = dto.AppliesToAllProducts,
                CreatedAt = DateTime.UtcNow,
                Items = dto.Items.Select(i => new OfferItem
                {
                    ProductId = i.ProductId,
                    RequiredQuantity = i.RequiredQuantity,
                    CreatedAt = DateTime.UtcNow
                }).ToList()
            };

            _context.Offers.Add(offer);
            await _context.SaveChangesAsync();

            var created = await _context.Offers
                .AsNoTracking()
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstAsync(o => o.Id == offer.Id);

            _logger.LogInformation("Offer created. OfferId: {OfferId}, StoreId: {StoreId}", created.Id, created.StoreId);

            return ToDto(created);
        }

        public async Task<OfferDto?> UpdateOfferAsync(Guid id, UpdateOfferDto dto)
        {
            var offer = await _context.Offers
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offer == null)
                return null;

            await EnsureCanManageStoreAsync(offer.StoreId);

            var effectiveType = dto.Type ?? offer.Type;
            var effectiveStartDate = dto.StartDate?.ToUniversalTime() ?? offer.StartDate;
            var effectiveEndDate = dto.EndDate?.ToUniversalTime() ?? offer.EndDate;
            var effectiveBundlePrice = dto.BundlePrice ?? offer.BundlePrice;
            var effectiveDiscountPercentage = dto.DiscountPercentage ?? offer.DiscountPercentage;
            var effectiveDiscountAmount = dto.DiscountAmount ?? offer.DiscountAmount;

            ValidateOfferInput(
                effectiveType,
                effectiveBundlePrice,
                effectiveDiscountPercentage,
                effectiveDiscountAmount,
                effectiveStartDate,
                effectiveEndDate);

            if (dto.Name != null) offer.Name = dto.Name.Trim();
            if (dto.Description != null) offer.Description = NormalizeNullable(dto.Description);
            if (dto.Type.HasValue) offer.Type = dto.Type.Value;
            if (dto.IsActive.HasValue) offer.IsActive = dto.IsActive.Value;
            if (dto.StartDate.HasValue) offer.StartDate = dto.StartDate.Value.ToUniversalTime();
            if (dto.EndDate.HasValue) offer.EndDate = dto.EndDate.Value.ToUniversalTime();
            if (dto.Priority.HasValue) offer.Priority = dto.Priority.Value;
            if (dto.BundlePrice.HasValue) offer.BundlePrice = dto.BundlePrice;
            if (dto.DiscountPercentage.HasValue) offer.DiscountPercentage = dto.DiscountPercentage;
            if (dto.DiscountAmount.HasValue) offer.DiscountAmount = dto.DiscountAmount;
            if (dto.AppliesToAllProducts.HasValue) offer.AppliesToAllProducts = dto.AppliesToAllProducts.Value;

            if (dto.Items != null)
            {
                if (dto.Items.Count == 0)
                    throw new InvalidOperationException("Offer must include at least one product.");

                await ValidateOfferItemsBelongToStoreAsync(offer.StoreId, dto.Items);

                _context.RemoveRange(offer.Items);
                offer.Items = dto.Items.Select(i => new OfferItem
                {
                    OfferId = offer.Id,
                    ProductId = i.ProductId,
                    RequiredQuantity = i.RequiredQuantity,
                    CreatedAt = DateTime.UtcNow
                }).ToList();
            }

            await _context.SaveChangesAsync();

            var updated = await _context.Offers
                .AsNoTracking()
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstAsync(o => o.Id == offer.Id);

            _logger.LogInformation("Offer updated. OfferId: {OfferId}, StoreId: {StoreId}", updated.Id, updated.StoreId);

            return ToDto(updated);
        }

        public async Task<bool> DeleteOfferAsync(Guid id)
        {
            var offer = await _context.Offers
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offer == null)
                return false;

            await EnsureCanManageStoreAsync(offer.StoreId);

            offer.IsDeleted = true;
            offer.IsActive = false;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Offer soft deleted. OfferId: {OfferId}, StoreId: {StoreId}", offer.Id, offer.StoreId);

            return true;
        }

        public async Task ValidateOfferUsageAsync(Guid storeId)
        {
            var canUseOffers = await _subscriptionService.CanStoreUseOffersAsync(storeId);

            if (!canUseOffers)
                throw new InvalidOperationException("هذه الميزة غير متاحة في باقتك");
        }

        private async Task EnsureCanManageStoreAsync(Guid storeId)
        {
            if (_currentUser.IsSuperAdmin)
                return;

            if (!_currentUser.IsStoreOwner || !_currentUser.UserId.HasValue)
                throw new UnauthorizedAccessException("You are not authorized to manage this store.");

            var ownsStore = await _storeOwnershipService.UserOwnsStoreAsync(storeId, _currentUser.UserId.Value);
            if (!ownsStore)
                throw new KeyNotFoundException("Store not found.");
        }

        private async Task<bool> CanCurrentUserManageStoreAsync(Guid storeId)
        {
            if (_currentUser.IsSuperAdmin)
                return true;

            if (!_currentUser.IsStoreOwner || !_currentUser.UserId.HasValue)
                return false;

            return await _storeOwnershipService.UserOwnsStoreAsync(storeId, _currentUser.UserId.Value);
        }

        private async Task ValidateOfferItemsBelongToStoreAsync(Guid storeId, IEnumerable<CreateOfferItemDto> items)
        {
            var itemList = items.ToList();

            var duplicateProductIds = itemList
                .GroupBy(i => i.ProductId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateProductIds.Count > 0)
                throw new InvalidOperationException("Offer contains duplicate products.");

            var productIds = itemList.Select(i => i.ProductId).ToList();

            var validProductCount = await _context.Products
                .AsNoTracking()
                .CountAsync(p => p.StoreId == storeId && productIds.Contains(p.Id));

            if (validProductCount != productIds.Count)
                throw new InvalidOperationException("All offer products must belong to the same store.");
        }

        private static void ValidateOfferInput(
            OfferType type,
            decimal? bundlePrice,
            decimal? discountPercentage,
            decimal? discountAmount,
            DateTime startDate,
            DateTime? endDate)
        {
            if (endDate.HasValue && endDate.Value < startDate)
                throw new InvalidOperationException("Offer EndDate cannot be before StartDate.");

            if (type == OfferType.BundleFixedPrice)
            {
                if (!bundlePrice.HasValue || bundlePrice.Value <= 0)
                    throw new InvalidOperationException("BundleFixedPrice offer requires BundlePrice > 0.");
            }

            if (type == OfferType.PercentageDiscount)
            {
                if (!discountPercentage.HasValue || discountPercentage.Value <= 0 || discountPercentage.Value > 100)
                    throw new InvalidOperationException("PercentageDiscount offer requires DiscountPercentage between 0 and 100.");
            }

            if (type == OfferType.FixedAmountDiscount)
            {
                if (!discountAmount.HasValue || discountAmount.Value <= 0)
                    throw new InvalidOperationException("FixedAmountDiscount offer requires DiscountAmount > 0.");
            }
        }

        private static string? NormalizeNullable(string? value)
        {
            if (value == null)
                return null;

            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static OfferDto ToDto(Models.Offers.Offer offer)
        {
            return new OfferDto
            {
                Id = offer.Id,
                StoreId = offer.StoreId,
                Name = offer.Name,
                Description = offer.Description,
                Type = offer.Type,
                IsActive = offer.IsActive,
                StartDate = offer.StartDate,
                EndDate = offer.EndDate,
                Priority = offer.Priority,
                BundlePrice = offer.BundlePrice,
                DiscountPercentage = offer.DiscountPercentage,
                DiscountAmount = offer.DiscountAmount,
                AppliesToAllProducts = offer.AppliesToAllProducts,
                CreatedAt = offer.CreatedAt,
                UpdatedAt = offer.UpdatedAt,
                Items = offer.Items.Select(i => new OfferItemDto
                {
                    Id = i.Id,
                    OfferId = i.OfferId,
                    ProductId = i.ProductId,
                    ProductName = i.Product?.Name ?? string.Empty,
                    RequiredQuantity = i.RequiredQuantity
                }).ToList()
            };
        }
    }
}
