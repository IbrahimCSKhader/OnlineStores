using Microsoft.EntityFrameworkCore;
using onlineStore.DTOs.Cart;
using onlineStore.Models.CartModels;
using onlineStore.Models.Enums;
using onlineStore.Models.Offers.Enums;
using onlineStore.Services.Pricing;

namespace onlineStore.Services.Cart;

public partial class CartService
{
    public async Task<CartDto> AddBundleAsync(Guid storeCustomerId, AddBundleToCartDto dto)
    {
        if (dto.Quantity <= 0 || dto.Quantity > 10000) throw new InvalidOperationException("كمية الباكيج غير صالحة");
        await EnsureActiveStoreCustomerAsync(storeCustomerId, dto.StoreId);
        if (await _context.Stores.AnyAsync(s => s.Id == dto.StoreId && s.IsCatalog))
            throw new InvalidOperationException("هذا كاتالوج للعرض فقط، الشراء غير متاح");
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            var offer = await _context.Offers.IgnoreQueryFilters().Where(o => !o.IsDeleted).AsNoTracking().Include(o => o.Items.Where(i => !i.IsDeleted)).ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == dto.OfferId && o.StoreId == dto.StoreId
                    && o.Type == OfferType.BundleFixedPrice && o.IsActive && o.StartDate <= now
                    && (!o.EndDate.HasValue || o.EndDate >= now));
            if (offer == null || offer.Items.Count == 0 || offer.BundlePrice is null or <= 0)
                throw new InvalidOperationException("الباكيج غير متاح حالياً");
            if (dto.Variants.Keys.Any(id => !offer.Items.Any(i => i.ProductId == id)))
                throw new InvalidOperationException("اختيار نسخة لمنتج خارج الباكيج غير مسموح");
            var cart = await GetOrCreateCartAsync(storeCustomerId, dto.StoreId);
            var groupId = Guid.NewGuid();
            var rows = new List<CartItem>();
            foreach (var component in offer.Items.OrderBy(i => i.ProductId))
            {
                var product = component.Product;
                if (product == null || product.IsDeleted || product.StoreId != dto.StoreId || product.Status != ProductStatus.Active)
                    throw new InvalidOperationException("أحد منتجات الباكيج غير متاح");
                var variant = await ResolveActiveVariantForCartAsync(product.Id,
                    dto.Variants.TryGetValue(product.Id, out var variantId) ? variantId : null);
                var quantity = checked(component.RequiredQuantity * dto.Quantity);
                var existing = await _context.CartItems.Where(i => i.CartId == cart.Id && !i.IsDeleted
                    && i.ProductId == product.Id && i.VariantId == variant.Id).SumAsync(i => i.Quantity);
                if (product.TrackInventory && (long)existing + quantity > variant.StockQuantity)
                    throw new InvalidOperationException($"الكمية المتاحة من {product.Name} لا تكفي للباكيج");
                rows.Add(new CartItem { CartId = cart.Id, ProductId = product.Id, Product = product,
                    VariantId = variant.Id, Variant = variant, Quantity = quantity,
                    BundleOfferId = offer.Id, BundleGroupId = groupId, BundleName = offer.Name,
                    BundleQuantity = dto.Quantity, BundleItemQuantity = component.RequiredQuantity });
            }
            BundleCartPricing.Allocate(rows, offer.BundlePrice.Value, dto.Quantity);
            // Query results are detached: attach components before adding rows to avoid inserting existing products.
            foreach (var row in rows)
            {
                row.Product = null!;
                row.Variant = null;
            }
            _context.CartItems.AddRange(rows);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return await ToDtoAsync((await LoadCartWithDetailsAsync(cart.Id, true))!);
        });
    }

    private async Task<CartDto> UpdateBundleAsync(CartItem item, int bundleQuantity)
    {
        if (bundleQuantity <= 0 || bundleQuantity > 10000) throw new InvalidOperationException("كمية الباكيج غير صالحة");
        var rows = await _context.CartItems.Include(i => i.Product).Include(i => i.Variant)
            .Where(i => i.CartId == item.CartId && !i.IsDeleted && i.BundleGroupId == item.BundleGroupId).ToListAsync();
        foreach (var row in rows)
        {
            var quantity = checked(row.BundleItemQuantity!.Value * bundleQuantity);
            var other = await _context.CartItems.Where(i => i.CartId == item.CartId && !i.IsDeleted
                && i.BundleGroupId != item.BundleGroupId && i.ProductId == row.ProductId && i.VariantId == row.VariantId)
                .SumAsync(i => i.Quantity);
            if (row.Product.TrackInventory && (long)quantity + other > (row.Variant?.StockQuantity ?? row.Product.StockQuantity))
                throw new InvalidOperationException($"الكمية المتاحة من {row.Product.Name} لا تكفي للباكيج");
            row.Quantity = quantity;
            row.BundleQuantity = bundleQuantity;
        }
        await BundleCartPricing.RefreshAsync(_context, item.Cart.StoreId, rows);
        await _context.SaveChangesAsync();
        return await ToDtoAsync((await LoadCartWithDetailsAsync(item.CartId, true))!);
    }
}
