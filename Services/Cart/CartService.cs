// Services/Cart/CartService.cs
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Cart;
using onlineStore.Models.CartModels;

namespace onlineStore.Services.Cart
{
    public class CartService : ICartService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CartService> _logger;

        public CartService(
            AppDbContext context,
            ILogger<CartService> logger)
        {
            _context = context;
            _logger = logger;
        }


        // ════════════════════════════════════════════════════
        // Get Cart
        // ════════════════════════════════════════════════════
        public async Task<CartDto> GetCartAsync(Guid userId, Guid storeId)
        {
            var cart = await GetOrCreateCartAsync(userId, storeId);
            return ToDto(cart);
        }


        // ════════════════════════════════════════════════════
        // Add To Cart
        // ════════════════════════════════════════════════════
        public async Task<CartDto> AddToCartAsync(Guid userId, AddToCartDto dto)
        {
            // ── جيب أو أنشئ الكارت ──
            var cart = await GetOrCreateCartAsync(userId, dto.StoreId);

            // ── 🔐 تحقق إن المنتج موجود وفي مخزون ──
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == dto.ProductId
                                       && p.StoreId == dto.StoreId);

            if (product == null)
                throw new Exception("المنتج غير موجود");

            // ── تحقق من المخزون ──
            var availableStock = product.StockQuantity;

            if (dto.VariantId.HasValue)
            {
                var variant = await _context.ProductVariants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Id == dto.VariantId);

                if (variant == null)
                    throw new Exception("النسخة غير موجودة");

                availableStock = variant.StockQuantity;
            }

            if (product.TrackInventory && availableStock < dto.Quantity)
                throw new Exception($"الكمية المتاحة {availableStock} فقط");

            // ── تحقق لو المنتج موجود بالكارت ──
            var existingItem = cart.Items.FirstOrDefault(i =>
                i.ProductId == dto.ProductId &&
                i.VariantId == dto.VariantId);

            if (existingItem != null)
            {
                // ── زيد الكمية ──
                var newQty = existingItem.Quantity + dto.Quantity;

                if (product.TrackInventory && availableStock < newQty)
                    throw new Exception($"الكمية المتاحة {availableStock} فقط");

                existingItem.Quantity = newQty;
            }
            else
            {
                // ── أضف عنصر جديد ──
                var unitPrice = dto.VariantId.HasValue
                    ? await GetVariantPriceAsync(dto.VariantId.Value, product.Price)
                    : product.Price;

                var cartItem = new CartItem
                {
                    CartId = cart.Id,
                    ProductId = dto.ProductId,
                    VariantId = dto.VariantId,
                    Quantity = dto.Quantity,
                    UnitPrice = unitPrice,
                    CreatedAt = DateTime.UtcNow
                };

                cart.Items.Add(cartItem);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Product {ProductId} added to cart for user {UserId}",
                dto.ProductId, userId);

            return ToDto(cart);
        }


        // ════════════════════════════════════════════════════
        // Update Cart Item
        // ════════════════════════════════════════════════════
        public async Task<CartDto> UpdateCartItemAsync(
            Guid userId, Guid cartItemId, UpdateCartItemDto dto)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                .Include(c => c.Items)
                    .ThenInclude(i => i.Variant)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
                throw new Exception("الكارت غير موجود");

            var item = cart.Items.FirstOrDefault(i => i.Id == cartItemId);
            if (item == null)
                throw new Exception("العنصر غير موجود في الكارت");

            // 🔐 تحقق من المخزون
            if (item.Product.TrackInventory)
            {
                var availableStock = item.Variant != null
                    ? item.Variant.StockQuantity
                    : item.Product.StockQuantity;

                if (availableStock < dto.Quantity)
                    throw new Exception($"الكمية المتاحة {availableStock} فقط");
            }

            item.Quantity = dto.Quantity;
            await _context.SaveChangesAsync();

            return ToDto(cart);
        }


        // ════════════════════════════════════════════════════
        // Remove From Cart
        // ════════════════════════════════════════════════════
        public async Task<CartDto> RemoveFromCartAsync(
            Guid userId, Guid cartItemId)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                .Include(c => c.Items)
                    .ThenInclude(i => i.Variant)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
                throw new Exception("الكارت غير موجود");

            // 🔐 تأكد إن العنصر يخص هاد الكارت
            var item = cart.Items.FirstOrDefault(i => i.Id == cartItemId);
            if (item == null)
                throw new Exception("العنصر غير موجود في الكارت");

            cart.Items.Remove(item);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Item {ItemId} removed from cart for user {UserId}",
                cartItemId, userId);

            return ToDto(cart);
        }


        // ════════════════════════════════════════════════════
        // Clear Cart
        // ════════════════════════════════════════════════════
        public async Task<bool> ClearCartAsync(Guid userId, Guid storeId)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId
                                       && c.StoreId == storeId);

            if (cart == null) return false;

            cart.Items.Clear();
            await _context.SaveChangesAsync();

            return true;
        }


        // ════════════════════════════════════════════════════
        // ⚡ Helper — Get Or Create Cart
        // ════════════════════════════════════════════════════
        private async Task<ShoppingCart> GetOrCreateCartAsync(
      Guid userId, Guid storeId)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                .Include(c => c.Items)
                    .ThenInclude(i => i.Variant)
                .FirstOrDefaultAsync(c => c.UserId == userId
                                       && c.StoreId == storeId);

            if (cart == null)
            {
                cart = new ShoppingCart  // ← غير هون
                {
                    UserId = userId,
                    StoreId = storeId,
                    CreatedAt = DateTime.UtcNow,
                    Items = new List<CartItem>()
                };

                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            return cart;
        }


        // ════════════════════════════════════════════════════
        // ⚡ Helper — Get Variant Price
        // ════════════════════════════════════════════════════
        private async Task<decimal> GetVariantPriceAsync(
            Guid variantId, decimal productPrice)
        {
            var variant = await _context.ProductVariants
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == variantId);

            return variant?.PriceOverride ?? productPrice;
        }


        // ════════════════════════════════════════════════════
        // Helper — ToDto
        // ════════════════════════════════════════════════════
        private static CartDto ToDto(ShoppingCart cart) => new()  // ← غير هون
        {
            Id = cart.Id,
            UserId = cart.UserId,
            StoreId = cart.StoreId,
            CreatedAt = cart.CreatedAt,
            Items = cart.Items?.Select(i => new CartItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.Product?.Name ?? "",
                ProductThumbnail = i.Product?.ThumbnailUrl,
                VariantId = i.VariantId,
                VariantName = i.Variant?.Name,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                AvailableStock = i.Variant != null
                    ? i.Variant.StockQuantity
                    : i.Product?.StockQuantity ?? 0
            }).ToList() ?? new()
        };
    }
}