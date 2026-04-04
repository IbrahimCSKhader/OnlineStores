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
            try
            {
                _logger.LogInformation(
                    "GetCartAsync started. UserId: {UserId}, StoreId: {StoreId}",
                    userId, storeId);

                var cart = await GetOrCreateCartAsync(userId, storeId);

                _logger.LogInformation(
                    "GetCartAsync completed successfully. CartId: {CartId}, ItemsCount: {ItemsCount}",
                    cart.Id, cart.Items?.Count ?? 0);

                return ToDto(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in GetCartAsync. UserId: {UserId}, StoreId: {StoreId}",
                    userId, storeId);

                throw;
            }
        }

        // ════════════════════════════════════════════════════
        // Add To Cart
        // ════════════════════════════════════════════════════
        public async Task<CartDto> AddToCartAsync(Guid userId, AddToCartDto dto)
        {
            try
            {
                _logger.LogInformation(
                    "AddToCartAsync started. UserId: {UserId}, StoreId: {StoreId}, ProductId: {ProductId}, VariantId: {VariantId}, Quantity: {Quantity}",
                    userId, dto?.StoreId, dto?.ProductId, dto?.VariantId, dto?.Quantity);

                if (dto == null)
                {
                    _logger.LogWarning("AddToCartAsync failed because dto is null. UserId: {UserId}", userId);
                    throw new Exception("بيانات الطلب غير صالحة");
                }

                if (dto.Quantity <= 0)
                {
                    _logger.LogWarning(
                        "AddToCartAsync failed because quantity <= 0. UserId: {UserId}, Quantity: {Quantity}",
                        userId, dto.Quantity);

                    throw new Exception("الكمية يجب أن تكون أكبر من صفر");
                }

                // 1) جيب أو أنشئ الكارت
                var cart = await GetOrCreateCartAsync(userId, dto.StoreId);

                _logger.LogInformation(
                    "Cart resolved successfully. CartId: {CartId}, UserId: {UserId}, StoreId: {StoreId}",
                    cart.Id, userId, dto.StoreId);

                // 2) تحقق من المنتج
                var product = await _context.Products
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == dto.ProductId
                                           && p.StoreId == dto.StoreId);

                if (product == null)
                {
                    _logger.LogWarning(
                        "Product not found. ProductId: {ProductId}, StoreId: {StoreId}",
                        dto.ProductId, dto.StoreId);

                    throw new Exception("المنتج غير موجود");
                }

                _logger.LogInformation(
                    "Product found. ProductId: {ProductId}, ProductName: {ProductName}, TrackInventory: {TrackInventory}, StockQuantity: {StockQuantity}, Price: {Price}",
                    product.Id, product.Name, product.TrackInventory, product.StockQuantity, product.Price);

                // 3) تحقق من النسخة إذا موجودة
                int availableStock = product.StockQuantity;
                decimal basePrice = product.Price;

                if (dto.VariantId.HasValue)
                {
                    var variant = await _context.ProductVariants
                        .AsNoTracking()
                        .FirstOrDefaultAsync(v => v.Id == dto.VariantId.Value);

                    if (variant == null)
                    {
                        _logger.LogWarning(
                            "Variant not found. VariantId: {VariantId}",
                            dto.VariantId.Value);

                        throw new Exception("النسخة غير موجودة");
                    }

                    if (variant.ProductId != dto.ProductId)
                    {
                        _logger.LogWarning(
                            "Variant does not belong to product. VariantId: {VariantId}, VariantProductId: {VariantProductId}, RequestedProductId: {RequestedProductId}",
                            variant.Id, variant.ProductId, dto.ProductId);

                        throw new Exception("النسخة لا تتبع هذا المنتج");
                    }

                    availableStock = variant.StockQuantity;
                    basePrice = variant.PriceOverride ?? product.Price;

                    _logger.LogInformation(
                        "Variant found. VariantId: {VariantId}, VariantName: {VariantName}, StockQuantity: {StockQuantity}, BasePrice: {BasePrice}",
                        variant.Id, variant.Name, variant.StockQuantity, basePrice);
                }
                else
                {
                    _logger.LogInformation(
                        "No variant selected. Using product price and stock. ProductId: {ProductId}, BasePrice: {BasePrice}, AvailableStock: {AvailableStock}",
                        product.Id, basePrice, availableStock);
                }

                // 4) ابحث عن العنصر الموجود مباشرة من جدول CartItems
                var existingItem = await _context.CartItems
                    .FirstOrDefaultAsync(i =>
                        i.CartId == cart.Id &&
                        i.ProductId == dto.ProductId &&
                        i.VariantId == dto.VariantId);

                if (existingItem != null)
                {
                    _logger.LogInformation(
                        "Existing cart item found. CartItemId: {CartItemId}, CurrentQuantity: {CurrentQuantity}",
                        existingItem.Id, existingItem.Quantity);

                    var newQuantity = existingItem.Quantity + dto.Quantity;

                    if (product.TrackInventory && availableStock < newQuantity)
                    {
                        _logger.LogWarning(
                            "Insufficient stock for existing cart item. AvailableStock: {AvailableStock}, RequestedNewQuantity: {RequestedNewQuantity}, CartItemId: {CartItemId}",
                            availableStock, newQuantity, existingItem.Id);

                        throw new Exception($"الكمية المتاحة {availableStock} فقط");
                    }

                    existingItem.Quantity = newQuantity;
                    existingItem.UpdatedAt = DateTime.UtcNow;

                    _logger.LogInformation(
                        "Existing cart item updated in memory. CartItemId: {CartItemId}, NewQuantity: {NewQuantity}",
                        existingItem.Id, existingItem.Quantity);
                }
                else
                {
                    if (product.TrackInventory && availableStock < dto.Quantity)
                    {
                        _logger.LogWarning(
                            "Insufficient stock for new cart item. AvailableStock: {AvailableStock}, RequestedQuantity: {RequestedQuantity}, ProductId: {ProductId}, VariantId: {VariantId}",
                            availableStock, dto.Quantity, dto.ProductId, dto.VariantId);

                        throw new Exception($"الكمية المتاحة {availableStock} فقط");
                    }

                    var unitPrice = await ApplyWholesaleDiscountIfExistsAsync(
                        userId,
                        dto.StoreId,
                        basePrice);

                    _logger.LogInformation(
                        "Final unit price resolved. UserId: {UserId}, StoreId: {StoreId}, ProductId: {ProductId}, VariantId: {VariantId}, UnitPrice: {UnitPrice}",
                        userId, dto.StoreId, dto.ProductId, dto.VariantId, unitPrice);

                    var newItem = new CartItem
                    {
                        Id = Guid.NewGuid(),
                        CartId = cart.Id,
                        ProductId = dto.ProductId,
                        VariantId = dto.VariantId,
                        Quantity = dto.Quantity,
                        UnitPrice = unitPrice,
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    };

                    await _context.CartItems.AddAsync(newItem);

                    _logger.LogInformation(
                        "New cart item added to DbContext explicitly. CartItemId: {CartItemId}, CartId: {CartId}, ProductId: {ProductId}, VariantId: {VariantId}, Quantity: {Quantity}",
                        newItem.Id, cart.Id, dto.ProductId, dto.VariantId, dto.Quantity);
                }

                _logger.LogInformation(
                    "Calling SaveChangesAsync in AddToCartAsync. UserId: {UserId}, StoreId: {StoreId}, ProductId: {ProductId}",
                    userId, dto.StoreId, dto.ProductId);

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "SaveChangesAsync completed successfully in AddToCartAsync. Reloading cart. CartId: {CartId}",
                    cart.Id);

                // 5) أعد تحميل الكارت بشكل نظيف بعد الحفظ
                var refreshedCart = await _context.Carts
                    .AsNoTracking()
                    .Include(c => c.Items.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.Product)
                    .Include(c => c.Items.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.Variant)
                    .FirstOrDefaultAsync(c => c.Id == cart.Id);

                if (refreshedCart == null)
                {
                    _logger.LogWarning(
                        "Cart disappeared after save. CartId: {CartId}",
                        cart.Id);

                    throw new Exception("تعذر تحميل الكارت بعد الحفظ");
                }

                _logger.LogInformation(
                    "AddToCartAsync completed successfully. CartId: {CartId}, ItemsCount: {ItemsCount}",
                    refreshedCart.Id, refreshedCart.Items?.Count ?? 0);

                return ToDto(refreshedCart);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in AddToCartAsync. UserId: {UserId}, StoreId: {StoreId}, ProductId: {ProductId}, VariantId: {VariantId}, Quantity: {Quantity}",
                    userId, dto?.StoreId, dto?.ProductId, dto?.VariantId, dto?.Quantity);

                throw;
            }
        }
        // ════════════════════════════════════════════════════
        // Update Cart Item
        // ════════════════════════════════════════════════════
        public async Task<CartDto> UpdateCartItemAsync(
            Guid userId,
            Guid cartItemId,
            UpdateCartItemDto dto)
        {
            try
            {
                _logger.LogInformation(
                    "UpdateCartItemAsync started. UserId: {UserId}, CartItemId: {CartItemId}, Quantity: {Quantity}",
                    userId, cartItemId, dto.Quantity);

                if (dto == null || dto.Quantity <= 0)
                {
                    _logger.LogWarning(
                        "UpdateCartItemAsync failed because quantity is invalid. UserId: {UserId}, CartItemId: {CartItemId}, Quantity: {Quantity}",
                        userId, cartItemId, dto?.Quantity);

                    throw new Exception("الكمية يجب أن تكون أكبر من صفر");
                }

                var cart = await _context.Carts
                    .Include(c => c.Items)
                        .ThenInclude(i => i.Product)
                    .Include(c => c.Items)
                        .ThenInclude(i => i.Variant)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart == null)
                {
                    _logger.LogWarning(
                        "Cart not found in UpdateCartItemAsync. UserId: {UserId}",
                        userId);

                    throw new Exception("الكارت غير موجود");
                }

                var item = cart.Items.FirstOrDefault(i => i.Id == cartItemId);
                if (item == null)
                {
                    _logger.LogWarning(
                        "Cart item not found in UpdateCartItemAsync. UserId: {UserId}, CartItemId: {CartItemId}",
                        userId, cartItemId);

                    throw new Exception("العنصر غير موجود في الكارت");
                }

                if (item.Product == null)
                {
                    _logger.LogWarning(
                        "Cart item product is null in UpdateCartItemAsync. CartItemId: {CartItemId}",
                        cartItemId);

                    throw new Exception("بيانات المنتج غير مكتملة");
                }

                if (item.Product.TrackInventory)
                {
                    var availableStock = item.Variant != null
                        ? item.Variant.StockQuantity
                        : item.Product.StockQuantity;

                    _logger.LogInformation(
                        "Stock check in UpdateCartItemAsync. CartItemId: {CartItemId}, AvailableStock: {AvailableStock}, RequestedQuantity: {RequestedQuantity}",
                        cartItemId, availableStock, dto.Quantity);

                    if (availableStock < dto.Quantity)
                    {
                        _logger.LogWarning(
                            "Insufficient stock in UpdateCartItemAsync. CartItemId: {CartItemId}, AvailableStock: {AvailableStock}, RequestedQuantity: {RequestedQuantity}",
                            cartItemId, availableStock, dto.Quantity);

                        throw new Exception($"الكمية المتاحة {availableStock} فقط");
                    }
                }

                item.Quantity = dto.Quantity;

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "UpdateCartItemAsync completed successfully. CartItemId: {CartItemId}, NewQuantity: {NewQuantity}",
                    cartItemId, item.Quantity);

                return ToDto(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in UpdateCartItemAsync. UserId: {UserId}, CartItemId: {CartItemId}, Quantity: {Quantity}",
                    userId, cartItemId, dto?.Quantity);

                throw;
            }
        }

        // ════════════════════════════════════════════════════
        // Remove From Cart
        // ════════════════════════════════════════════════════
        public async Task<CartDto> RemoveFromCartAsync(
            Guid userId,
            Guid cartItemId)
        {
            try
            {
                _logger.LogInformation(
                    "RemoveFromCartAsync started. UserId: {UserId}, CartItemId: {CartItemId}",
                    userId, cartItemId);

                var cart = await _context.Carts
                    .Include(c => c.Items)
                        .ThenInclude(i => i.Product)
                    .Include(c => c.Items)
                        .ThenInclude(i => i.Variant)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart == null)
                {
                    _logger.LogWarning(
                        "Cart not found in RemoveFromCartAsync. UserId: {UserId}",
                        userId);

                    throw new Exception("الكارت غير موجود");
                }

                var item = cart.Items.FirstOrDefault(i => i.Id == cartItemId);
                if (item == null)
                {
                    _logger.LogWarning(
                        "Cart item not found in RemoveFromCartAsync. UserId: {UserId}, CartItemId: {CartItemId}",
                        userId, cartItemId);

                    throw new Exception("العنصر غير موجود في الكارت");
                }

                cart.Items.Remove(item);

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "RemoveFromCartAsync completed successfully. UserId: {UserId}, CartItemId: {CartItemId}",
                    userId, cartItemId);

                return ToDto(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in RemoveFromCartAsync. UserId: {UserId}, CartItemId: {CartItemId}",
                    userId, cartItemId);

                throw;
            }
        }

        // ════════════════════════════════════════════════════
        // Clear Cart
        // ════════════════════════════════════════════════════
        public async Task<bool> ClearCartAsync(Guid userId, Guid storeId)
        {
            try
            {
                _logger.LogInformation(
                    "ClearCartAsync started. UserId: {UserId}, StoreId: {StoreId}",
                    userId, storeId);

                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == userId
                                           && c.StoreId == storeId);

                if (cart == null)
                {
                    _logger.LogWarning(
                        "Cart not found in ClearCartAsync. UserId: {UserId}, StoreId: {StoreId}",
                        userId, storeId);

                    return false;
                }

                cart.Items.Clear();
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "ClearCartAsync completed successfully. UserId: {UserId}, StoreId: {StoreId}",
                    userId, storeId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in ClearCartAsync. UserId: {UserId}, StoreId: {StoreId}",
                    userId, storeId);

                throw;
            }
        }

        // ════════════════════════════════════════════════════
        // Helper — Get Or Create Cart
        // ════════════════════════════════════════════════════
        private async Task<ShoppingCart> GetOrCreateCartAsync(Guid userId, Guid storeId)
        {
            try
            {
                _logger.LogInformation(
                    "GetOrCreateCartAsync started. UserId: {UserId}, StoreId: {StoreId}",
                    userId, storeId);

                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.StoreId == storeId);

                if (cart != null)
                {
                    _logger.LogInformation(
                        "Existing cart found. CartId: {CartId}, ItemsCount: {ItemsCount}",
                        cart.Id, cart.Items?.Count ?? 0);

                    return cart;
                }

                cart = new ShoppingCart
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    StoreId = storeId,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false,
                    Items = new List<CartItem>()
                };

                await _context.Carts.AddAsync(cart);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "New cart created successfully. CartId: {CartId}, UserId: {UserId}, StoreId: {StoreId}",
                    cart.Id, userId, storeId);

                return cart;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in GetOrCreateCartAsync. UserId: {UserId}, StoreId: {StoreId}",
                    userId, storeId);

                throw;
            }
        }
        // ════════════════════════════════════════════════════
        // Helper — Get Variant Price
        // ════════════════════════════════════════════════════
        private async Task<decimal> GetVariantPriceAsync(
            Guid variantId,
            decimal productPrice)
        {
            try
            {
                _logger.LogInformation(
                    "GetVariantPriceAsync started. VariantId: {VariantId}, ProductPrice: {ProductPrice}",
                    variantId, productPrice);

                var variant = await _context.ProductVariants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Id == variantId);

                if (variant == null)
                {
                    _logger.LogWarning(
                        "Variant not found in GetVariantPriceAsync. VariantId: {VariantId}. Fallback to product price.",
                        variantId);

                    return productPrice;
                }

                var resolvedPrice = variant.PriceOverride ?? productPrice;

                _logger.LogInformation(
                    "GetVariantPriceAsync completed. VariantId: {VariantId}, ResolvedPrice: {ResolvedPrice}",
                    variantId, resolvedPrice);

                return resolvedPrice;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in GetVariantPriceAsync. VariantId: {VariantId}, ProductPrice: {ProductPrice}",
                    variantId, productPrice);

                throw;
            }
        }

        // ════════════════════════════════════════════════════
        // Helper — Apply Wholesale Discount
        // ════════════════════════════════════════════════════
        private async Task<decimal> ApplyWholesaleDiscountIfExistsAsync(
            Guid userId,
            Guid storeId,
            decimal price)
        {
            try
            {
                _logger.LogInformation(
                    "ApplyWholesaleDiscountIfExistsAsync started. UserId: {UserId}, StoreId: {StoreId}, OriginalPrice: {Price}",
                    userId, storeId, price);

                var discount = await _context.CustomerStores
                    .AsNoTracking()
                    .Where(x => x.StoreId == storeId
                             && x.CustomerId == userId
                             && x.IsActive)
                    .Select(x => x.DiscountPercentage)
                    .FirstOrDefaultAsync();

                _logger.LogInformation(
                    "Wholesale discount query completed. UserId: {UserId}, StoreId: {StoreId}, DiscountPercentage: {Discount}",
                    userId, storeId, discount);

                if (discount <= 0)
                {
                    _logger.LogInformation(
                        "No wholesale discount applied. Returning original price: {Price}",
                        price);

                    return price;
                }

                var discountedPrice = price - (price * discount / 100m);

                _logger.LogInformation(
                    "Wholesale discount applied successfully. OriginalPrice: {OriginalPrice}, DiscountPercentage: {Discount}, FinalPrice: {FinalPrice}",
                    price, discount, discountedPrice);

                return discountedPrice;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in ApplyWholesaleDiscountIfExistsAsync. UserId: {UserId}, StoreId: {StoreId}, Price: {Price}",
                    userId, storeId, price);

                throw;
            }
        }

        // ════════════════════════════════════════════════════
        // Helper — ToDto
        // ════════════════════════════════════════════════════
        private static CartDto ToDto(ShoppingCart cart) => new()
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