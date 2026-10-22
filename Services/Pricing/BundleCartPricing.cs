using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.Models.CartModels;
using onlineStore.Models.Offers.Enums;

namespace onlineStore.Services.Pricing;

public static class BundleCartPricing
{
    public static void Allocate(IReadOnlyList<CartItem> rows, decimal price, int quantity)
    {
        if (rows.Count == 0 || quantity <= 0 || price <= 0)
            throw new InvalidOperationException("بيانات الباكيج غير صالحة");
        var total = decimal.Round(price * quantity, 2, MidpointRounding.AwayFromZero);
        var weights = rows.Select(i => Math.Max(0m, (i.Variant?.Price ?? i.Product.Price) * i.Quantity)).ToArray();
        var weight = weights.Sum();
        decimal allocated = 0m;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var amount = index == rows.Count - 1 ? total - allocated
                : decimal.Floor(total * (weight > 0 ? weights[index] / weight : 1m / rows.Count) * 100m) / 100m;
            row.BundleLineTotal = amount;
            row.BundlePrice = price;
            row.BundleQuantity = quantity;
            row.UnitPrice = decimal.Round(amount / row.Quantity, 2);
            allocated += amount;
        }
    }

    public static async Task RefreshAsync(AppDbContext db, Guid storeId, IReadOnlyCollection<CartItem> items, bool strict = true)
    {
        var ids = items.Where(i => i.BundleOfferId.HasValue).Select(i => i.BundleOfferId!.Value).Distinct().ToList();
        if (ids.Count == 0) return;
        var offers = await db.Offers.AsNoTracking().Include(o => o.Items)
            .Where(o => o.StoreId == storeId && ids.Contains(o.Id)).ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var group in items.Where(i => i.BundleGroupId.HasValue).GroupBy(i => i.BundleGroupId))
        {
            var rows = group.OrderBy(i => i.ProductId).ToList();
            var first = rows[0];
            var offer = offers.FirstOrDefault(o => o.Id == first.BundleOfferId);
            var valid = offer != null && offer.Type == OfferType.BundleFixedPrice && offer.IsActive
                && offer.StartDate <= now && (!offer.EndDate.HasValue || offer.EndDate >= now)
                && offer.BundlePrice > 0 && offer.Items.Count == rows.Count
                && first.BundleQuantity > 0
                && rows.All(r => r.BundleOfferId == offer.Id && r.BundleQuantity == first.BundleQuantity
                    && r.Product != null && r.Product.StoreId == storeId && !r.Product.IsDeleted
                    && r.Product.Status == onlineStore.Models.Enums.ProductStatus.Active
                    && offer.Items.Any(i => i.ProductId == r.ProductId && i.RequiredQuantity == r.BundleItemQuantity)
                    && r.Quantity == checked(r.BundleQuantity * r.BundleItemQuantity));
            if (!valid)
            {
                if (strict) throw new InvalidOperationException($"الباكيج {first.BundleName} تغير أو لم يعد متاحاً، يرجى حذفه وإضافته من جديد");
                continue;
            }
            foreach (var row in rows) row.BundleName = offer!.Name;
            Allocate(rows, offer!.BundlePrice!.Value, first.BundleQuantity!.Value);
        }
    }
}
