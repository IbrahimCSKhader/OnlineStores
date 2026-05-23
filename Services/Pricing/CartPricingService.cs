using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Cart;
using onlineStore.Models.CartModels;
using onlineStore.Models.Offers.Enums;

namespace onlineStore.Services.Pricing
{
    public class CartPricingService : ICartPricingService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CartPricingService> _logger;

        public CartPricingService(AppDbContext context, ILogger<CartPricingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CartPricingDto> CalculatePricingAsync(
            Guid storeId,
            IReadOnlyCollection<CartItem> items,
            decimal customerDiscountPercentage = 0m,
            CancellationToken cancellationToken = default)
        {
            var subtotal = items.Sum(i => i.UnitPrice * i.Quantity);
            if (subtotal <= 0 || items.Count == 0)
            {
                return new CartPricingDto
                {
                    Subtotal = subtotal,
                    Discount = 0,
                    FinalTotal = subtotal
                };
            }

            if (customerDiscountPercentage > 0m)
            {
                _logger.LogInformation(
                    "Cart pricing skipped store offers because a store-customer discount is active. StoreId: {StoreId}, CustomerDiscountPercentage: {CustomerDiscountPercentage}, Subtotal: {Subtotal}",
                    storeId,
                    customerDiscountPercentage,
                    subtotal);

                return new CartPricingDto
                {
                    Subtotal = decimal.Round(subtotal, 2),
                    Discount = 0,
                    FinalTotal = decimal.Round(subtotal, 2)
                };
            }

            var productIds = items
                .Select(i => i.ProductId)
                .Distinct()
                .ToList();

            var now = DateTime.UtcNow;
            var offers = await _context.Offers
                .AsNoTracking()
                .Include(o => o.Items)
                .Where(o => o.StoreId == storeId
                            && o.IsActive
                            && o.StartDate <= now
                            && (!o.EndDate.HasValue || o.EndDate >= now)
                            && (o.AppliesToAllProducts || o.Items.Any(i => productIds.Contains(i.ProductId))))
                .OrderByDescending(o => o.Priority)
                .ThenBy(o => o.CreatedAt)
                .ToListAsync(cancellationToken);

            if (offers.Count == 0)
            {
                return new CartPricingDto
                {
                    Subtotal = subtotal,
                    Discount = 0,
                    FinalTotal = subtotal
                };
            }

            var groupedByProduct = items
                .GroupBy(i => i.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => new ProductCartAggregate(
                        g.Sum(x => x.Quantity),
                        g.Sum(x => x.UnitPrice * x.Quantity)));

            var totalDiscount = 0m;
            var appliedOffers = new List<AppliedOfferDto>();

            foreach (var offer in offers)
            {
                var discount = offer.Type switch
                {
                    OfferType.BundleFixedPrice => CalculateBundleDiscount(offer.BundlePrice, offer.Items, groupedByProduct),
                    OfferType.PercentageDiscount => CalculatePercentageDiscount(offer.DiscountPercentage, offer.AppliesToAllProducts, offer.Items, subtotal, groupedByProduct),
                    OfferType.FixedAmountDiscount => CalculateFixedAmountDiscount(offer.DiscountAmount, offer.AppliesToAllProducts, offer.Items, subtotal, groupedByProduct),
                    _ => 0m
                };

                if (discount <= 0)
                    continue;

                totalDiscount += discount;
                appliedOffers.Add(new AppliedOfferDto
                {
                    OfferId = offer.Id,
                    OfferName = offer.Name,
                    DiscountAmount = decimal.Round(discount, 2)
                });
            }

            if (totalDiscount > subtotal)
                totalDiscount = subtotal;

            var finalTotal = subtotal - totalDiscount;

            _logger.LogInformation(
                "Cart pricing calculated. StoreId: {StoreId}, Subtotal: {Subtotal}, Discount: {Discount}, FinalTotal: {FinalTotal}, AppliedOffers: {AppliedOffers}",
                storeId,
                subtotal,
                totalDiscount,
                finalTotal,
                appliedOffers.Count);

            return new CartPricingDto
            {
                Subtotal = decimal.Round(subtotal, 2),
                Discount = decimal.Round(totalDiscount, 2),
                FinalTotal = decimal.Round(finalTotal, 2),
                AppliedOffers = appliedOffers
            };
        }

        private static decimal CalculateBundleDiscount(
            decimal? bundlePrice,
            ICollection<Models.Offers.OfferItem> offerItems,
            IReadOnlyDictionary<Guid, ProductCartAggregate> groupedByProduct)
        {
            if (!bundlePrice.HasValue || bundlePrice.Value <= 0 || offerItems.Count == 0)
                return 0m;

            var bundleCount = int.MaxValue;

            foreach (var item in offerItems)
            {
                if (!groupedByProduct.TryGetValue(item.ProductId, out var cartItem))
                    return 0m;

                var possible = cartItem.Quantity / item.RequiredQuantity;
                if (possible <= 0)
                    return 0m;

                if (possible < bundleCount)
                    bundleCount = possible;
            }

            if (bundleCount <= 0 || bundleCount == int.MaxValue)
                return 0m;

            var regularBundleUnitPrice = 0m;
            foreach (var item in offerItems)
            {
                var cartItem = groupedByProduct[item.ProductId];
                var averageUnitPrice = cartItem.Quantity <= 0 ? 0m : cartItem.Total / cartItem.Quantity;
                regularBundleUnitPrice += averageUnitPrice * item.RequiredQuantity;
            }

            var regularPrice = regularBundleUnitPrice * bundleCount;
            var bundleTotal = bundlePrice.Value * bundleCount;

            return regularPrice > bundleTotal ? regularPrice - bundleTotal : 0m;
        }

        private static decimal CalculatePercentageDiscount(
            decimal? percentage,
            bool appliesToAllProducts,
            ICollection<Models.Offers.OfferItem> offerItems,
            decimal subtotal,
            IReadOnlyDictionary<Guid, ProductCartAggregate> groupedByProduct)
        {
            if (!percentage.HasValue || percentage.Value <= 0 || percentage.Value > 100)
                return 0m;

            var baseAmount = appliesToAllProducts
                ? subtotal
                : offerItems
                    .Select(i => i.ProductId)
                    .Distinct()
                    .Where(groupedByProduct.ContainsKey)
                    .Sum(productId => (decimal)groupedByProduct[productId].Total);

            if (baseAmount <= 0)
                return 0m;

            return baseAmount * (percentage.Value / 100m);
        }

        private static decimal CalculateFixedAmountDiscount(
            decimal? discountAmount,
            bool appliesToAllProducts,
            ICollection<Models.Offers.OfferItem> offerItems,
            decimal subtotal,
            IReadOnlyDictionary<Guid, ProductCartAggregate> groupedByProduct)
        {
            if (!discountAmount.HasValue || discountAmount.Value <= 0)
                return 0m;

            var baseAmount = appliesToAllProducts
                ? subtotal
                : offerItems
                    .Select(i => i.ProductId)
                    .Distinct()
                    .Where(groupedByProduct.ContainsKey)
                    .Sum(productId => (decimal)groupedByProduct[productId].Total);

            if (baseAmount <= 0)
                return 0m;

            return discountAmount.Value > baseAmount ? baseAmount : discountAmount.Value;
        }

        private sealed record ProductCartAggregate(int Quantity, decimal Total);
    }
}
