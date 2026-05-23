using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Cart;
using onlineStore.Models.CartModels;
using onlineStore.Services.Pricing;

namespace onlineStore.Services.Cart
{
    public class CartService : ICartService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CartService> _logger;
        private readonly ICartPricingService _cartPricingService;

        public CartService(
            AppDbContext context,
            ILogger<CartService> logger,
            ICartPricingService cartPricingService)
        {
            _context = context;
            _logger = logger;
            _cartPricingService = cartPricingService;
        }

        // ════════════════════════════════════════════════════
        // Get Cart
        // ════════════════════════════════════════════════════
        public async Task<CartDto> GetCartAsync(Guid storeCustomerId, Guid storeId)
        {
            try
            {
                _logger.LogInformation(
                    "GetCartAsync started. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                    storeCustomerId, storeId);

                var cart = await GetOrCreateCartAsync(storeCustomerId, storeId);
                var detailedCart = await LoadCartWithDetailsAsync(cart.Id, asNoTracking: true)
                    ?? throw new Exception("تعذر تحميل السلة");

                _logger.LogInformation(
                    "GetCartAsync completed successfully. CartId: {CartId}, ItemsCount: {ItemsCount}",
                    detailedCart.Id, detailedCart.Items?.Count ?? 0);

                return await ToDtoAsync(detailedCart);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in GetCartAsync. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                    storeCustomerId, storeId);

                throw;
            }
        }

        // ════════════════════════════════════════════════════
        // Add To Cart
        // ════════════════════════════════════════════════════
        public async Task<CartDto> AddToCartAsync(Guid storeCustomerId, AddToCartDto dto)
        {
            try
            {
                _logger.LogInformation(
                    "AddToCartAsync started. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}, ProductId: {ProductId}, VariantId: {VariantId}, Quantity: {Quantity}",
                    storeCustomerId, dto?.StoreId, dto?.ProductId, dto?.VariantId, dto?.Quantity);

                if (dto == null)
                {
                    _logger.LogWarning("AddToCartAsync failed because dto is null. StoreCustomerId: {StoreCustomerId}", storeCustomerId);
                    throw new Exception("بيانات الطلب غير صالحة");
                }

                if (dto.Quantity <= 0)
                {
                    _logger.LogWarning(
                        "AddToCartAsync failed because quantity <= 0. UserId: {UserId}, Quantity: {Quantity}",
                        storeCustomerId, dto.Quantity);

                    throw new Exception("الكمية يجب أن تكون أكبر من صفر");
                }

                // 1) جيب أو أنشئ الكارت
                var cart = await GetOrCreateCartAsync(storeCustomerId, dto.StoreId);

                _logger.LogInformation(
                    "Cart resolved successfully. CartId: {CartId}, StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                    cart.Id, storeCustomerId, dto.StoreId);

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
                        i.VariantId == dto.VariantId &&
                        !i.IsDeleted);

                if (existingItem != null)
                {
                    _logger.LogInformation(
                        "Existing cart item found. CartItemId: {CartItemId}, CurrentQuantity: {CurrentQuantity}, ExistingUnitPrice: {ExistingUnitPrice}, BasePrice: {BasePrice}",
                        existingItem.Id, existingItem.Quantity, existingItem.UnitPrice, basePrice);

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
                        "Existing cart item updated in memory. CartItemId: {CartItemId}, NewQuantity: {NewQuantity}, UnitPricePreserved: {UnitPrice}",
                        existingItem.Id, existingItem.Quantity, existingItem.UnitPrice);
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
                        storeCustomerId,
                        dto.StoreId,
                        basePrice,
                        product.CompareAtPrice,
                        dto.VariantId.HasValue);

                    _logger.LogInformation(
                        "Final unit price resolved. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}, ProductId: {ProductId}, VariantId: {VariantId}, UnitPrice: {UnitPrice}",
                        storeCustomerId, dto.StoreId, dto.ProductId, dto.VariantId, unitPrice);

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
                    "Calling SaveChangesAsync in AddToCartAsync. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}, ProductId: {ProductId}",
                    storeCustomerId, dto.StoreId, dto.ProductId);

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "SaveChangesAsync completed successfully in AddToCartAsync. Reloading cart. CartId: {CartId}",
                    cart.Id);

                // 5) أعد تحميل الكارت بشكل نظيف بعد الحفظ
                var refreshedCart = await LoadCartWithDetailsAsync(cart.Id, asNoTracking: true);

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

                return await ToDtoAsync(refreshedCart);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in AddToCartAsync. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}, ProductId: {ProductId}, VariantId: {VariantId}, Quantity: {Quantity}",
                    storeCustomerId, dto?.StoreId, dto?.ProductId, dto?.VariantId, dto?.Quantity);

                throw;
            }
        }
        // ════════════════════════════════════════════════════
        // Update Cart Item
        // ════════════════════════════════════════════════════
        public async Task<CartDto> UpdateCartItemAsync(
            Guid storeCustomerId,
            Guid cartItemId,
            UpdateCartItemDto dto)
        {
            try
            {
                _logger.LogInformation(
                    "UpdateCartItemAsync started. StoreCustomerId: {StoreCustomerId}, CartItemId: {CartItemId}, Quantity: {Quantity}",
                    storeCustomerId, cartItemId, dto.Quantity);

                if (dto == null || dto.Quantity <= 0)
                {
                    _logger.LogWarning(
                        "UpdateCartItemAsync failed because quantity is invalid. UserId: {UserId}, CartItemId: {CartItemId}, Quantity: {Quantity}",
                        storeCustomerId, cartItemId, dto?.Quantity);

                    throw new Exception("الكمية يجب أن تكون أكبر من صفر");
                }

                var item = await _context.CartItems
                    .Include(i => i.Cart)
                    .Include(i => i.Product)
                    .Include(i => i.Variant)
                    .FirstOrDefaultAsync(i =>
                        i.Id == cartItemId &&
                        !i.IsDeleted &&
                        i.Cart.StoreCustomerId == storeCustomerId);

                if (item == null)
                {
                    _logger.LogWarning(
                        "Cart item not found in UpdateCartItemAsync. StoreCustomerId: {StoreCustomerId}, CartItemId: {CartItemId}",
                        storeCustomerId, cartItemId);

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

                var refreshedCart = await LoadCartWithDetailsAsync(item.CartId, asNoTracking: true)
                    ?? throw new Exception("تعذر تحميل السلة بعد تحديث العنصر");

                _logger.LogInformation(
                    "UpdateCartItemAsync completed successfully. CartItemId: {CartItemId}, CartId: {CartId}, StoreId: {StoreId}, NewQuantity: {NewQuantity}",
                    cartItemId, item.CartId, item.Cart.StoreId, item.Quantity);

                return await ToDtoAsync(refreshedCart);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in UpdateCartItemAsync. StoreCustomerId: {StoreCustomerId}, CartItemId: {CartItemId}, Quantity: {Quantity}",
                    storeCustomerId, cartItemId, dto?.Quantity);

                throw;
            }
        }

        // ════════════════════════════════════════════════════
        // Remove From Cart
        // ════════════════════════════════════════════════════
        public async Task<CartDto> RemoveFromCartAsync(
            Guid storeCustomerId,
            Guid cartItemId)
        {
            try
            {
                _logger.LogInformation(
                    "RemoveFromCartAsync started. StoreCustomerId: {StoreCustomerId}, CartItemId: {CartItemId}",
                    storeCustomerId, cartItemId);

                var item = await _context.CartItems
                    .Include(i => i.Cart)
                    .Include(i => i.Product)
                    .Include(i => i.Variant)
                    .FirstOrDefaultAsync(i =>
                        i.Id == cartItemId &&
                        !i.IsDeleted &&
                        i.Cart.StoreCustomerId == storeCustomerId);

                if (item == null)
                {
                    _logger.LogWarning(
                        "Cart item not found in RemoveFromCartAsync. StoreCustomerId: {StoreCustomerId}, CartItemId: {CartItemId}",
                        storeCustomerId, cartItemId);

                    throw new Exception("العنصر غير موجود في الكارت");
                }

                var cartId = item.CartId;
                var cartStoreId = item.Cart.StoreId;
                _context.CartItems.Remove(item);

                await _context.SaveChangesAsync();

                var refreshedCart = await LoadCartWithDetailsAsync(cartId, asNoTracking: true)
                    ?? throw new Exception("تعذر تحميل السلة بعد حذف العنصر");

                _logger.LogInformation(
                    "RemoveFromCartAsync completed successfully. StoreCustomerId: {StoreCustomerId}, CartItemId: {CartItemId}, CartId: {CartId}, StoreId: {StoreId}",
                    storeCustomerId, cartItemId, cartId, cartStoreId);

                return await ToDtoAsync(refreshedCart);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in RemoveFromCartAsync. StoreCustomerId: {StoreCustomerId}, CartItemId: {CartItemId}",
                    storeCustomerId, cartItemId);

                throw;
            }
        }

        // ════════════════════════════════════════════════════
        // Clear Cart
        // ════════════════════════════════════════════════════
        public async Task<bool> ClearCartAsync(Guid storeCustomerId, Guid storeId)
        {
            try
            {
                _logger.LogInformation(
                    "ClearCartAsync started. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                    storeCustomerId, storeId);

                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.StoreCustomerId == storeCustomerId
                                           && c.StoreId == storeId);

                if (cart == null)
                {
                    _logger.LogWarning(
                        "Cart not found in ClearCartAsync. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                        storeCustomerId, storeId);

                    return false;
                }

                var removedItemsCount = cart.Items.Count;
                _context.CartItems.RemoveRange(cart.Items);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "ClearCartAsync completed successfully. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}, RemovedItemsCount: {RemovedItemsCount}",
                    storeCustomerId, storeId, removedItemsCount);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in ClearCartAsync. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                    storeCustomerId, storeId);

                throw;
            }
        }

        // ════════════════════════════════════════════════════
        // Helper — Get Or Create Cart
        // ════════════════════════════════════════════════════
        private async Task<ShoppingCart> GetOrCreateCartAsync(Guid storeCustomerId, Guid storeId)
        {
            try
            {
                await EnsureActiveStoreCustomerAsync(storeCustomerId, storeId);

                _logger.LogInformation(
                    "GetOrCreateCartAsync started. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                    storeCustomerId, storeId);

                var carts = await BuildCartQuery(asNoTracking: false)
                    .Where(c => c.StoreCustomerId == storeCustomerId && c.StoreId == storeId)
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync();

                if (carts.Count > 1)
                {
                    _logger.LogWarning(
                        "Duplicate carts detected for StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}, CartIds: {@CartIds}",
                        storeCustomerId,
                        storeId,
                        carts.Select(c => c.Id).ToList());

                    var consolidatedCart = await ConsolidateDuplicateCartsAsync(carts);

                    _logger.LogInformation(
                        "Duplicate carts consolidated. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}, PrimaryCartId: {PrimaryCartId}, ItemsCount: {ItemsCount}",
                        storeCustomerId,
                        storeId,
                        consolidatedCart.Id,
                        consolidatedCart.Items.Count);

                    return consolidatedCart;
                }

                var cart = carts.FirstOrDefault();

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
                    StoreCustomerId = storeCustomerId,
                    StoreId = storeId,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false,
                    Items = new List<CartItem>()
                };

                await _context.Carts.AddAsync(cart);
                await _context.SaveChangesAsync();

                var createdCart = await LoadCartWithDetailsAsync(cart.Id, asNoTracking: false)
                    ?? cart;

                _logger.LogInformation(
                    "New cart created successfully. CartId: {CartId}, StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                    createdCart.Id, storeCustomerId, storeId);

                return createdCart;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in GetOrCreateCartAsync. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                    storeCustomerId, storeId);

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
            Guid storeCustomerId,
            Guid storeId,
            decimal price,
            decimal? compareAtPrice = null,
            bool variantPriceApplied = false)
        {
            try
            {
                _logger.LogInformation(
                    "ApplyWholesaleDiscountIfExistsAsync started. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}, OriginalPrice: {Price}",
                    storeCustomerId, storeId, price);

                var discount = await GetStoreCustomerDiscountPercentageAsync(storeCustomerId, storeId);

                _logger.LogInformation(
                    "Wholesale discount query completed. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}, DiscountPercentage: {Discount}",
                    storeCustomerId, storeId, discount);

                if (discount <= 0)
                {
                    _logger.LogInformation(
                        "No wholesale discount applied. Returning original price: {Price}",
                        price);

                    return price;
                }

                var priceBeforeDiscount = ResolvePriceBeforeStoreCustomerDiscount(
                    price,
                    compareAtPrice,
                    variantPriceApplied);
                var discountedPrice = priceBeforeDiscount - (priceBeforeDiscount * discount / 100m);

                _logger.LogInformation(
                    "Wholesale discount applied successfully. OriginalPrice: {OriginalPrice}, PriceBeforeStoreCustomerDiscount: {PriceBeforeDiscount}, DiscountPercentage: {Discount}, FinalPrice: {FinalPrice}",
                    price, priceBeforeDiscount, discount, discountedPrice);

                return discountedPrice;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in ApplyWholesaleDiscountIfExistsAsync. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}, Price: {Price}",
                    storeCustomerId, storeId, price);

                throw;
            }
        }

        // ════════════════════════════════════════════════════
        // Helper — ToDto
        // ════════════════════════════════════════════════════
        private async Task<CartDto> ToDtoAsync(ShoppingCart cart)
        {
            _logger.LogInformation(
                "CartService.ToDtoAsync started. CartId: {CartId}, StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}, RawItemsCount: {ItemsCount}",
                cart.Id,
                cart.StoreCustomerId,
                cart.StoreId,
                cart.Items?.Count ?? 0);

            _logger.LogDebug(
                "CartService.ToDtoAsync item snapshot for CartId: {CartId} => {@Items}",
                cart.Id,
                cart.Items?.Select(i => new
                {
                    i.Id,
                    i.ProductId,
                    ProductName = i.Product?.Name,
                    i.VariantId,
                    VariantName = i.Variant?.Name,
                    i.Quantity,
                    i.UnitPrice
                }).ToList());

            var items = cart.Items?.Select(i => new CartItemDto
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
            }).ToList() ?? new List<CartItemDto>();
            var customerDiscountPercentage = await GetStoreCustomerDiscountPercentageAsync(
                cart.StoreCustomerId,
                cart.StoreId);

            var pricing = await _cartPricingService.CalculatePricingAsync(
                cart.StoreId,
                cart.Items?.ToList() ?? new List<CartItem>(),
                customerDiscountPercentage);

            _logger.LogInformation(
                "CartService.ToDtoAsync pricing calculated. CartId: {CartId}, Subtotal: {Subtotal}, Discount: {Discount}, FinalTotal: {FinalTotal}, AppliedOffersCount: {AppliedOffersCount}",
                cart.Id,
                pricing.Subtotal,
                pricing.Discount,
                pricing.FinalTotal,
                pricing.AppliedOffers.Count);

            return new CartDto
            {
                Id = cart.Id,
                StoreCustomerId = cart.StoreCustomerId,
                StoreId = cart.StoreId,
                CreatedAt = cart.CreatedAt,
                Items = items,
                Subtotal = pricing.Subtotal,
                Discount = pricing.Discount,
                FinalTotal = pricing.FinalTotal,
                AppliedOffers = pricing.AppliedOffers
            };
        }

        private IQueryable<ShoppingCart> BuildCartQuery(bool asNoTracking)
        {
            var query = _context.Carts
                .Include(c => c.Items.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.Product)
                .Include(c => c.Items.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.Variant);

            return asNoTracking ? query.AsNoTracking() : query;
        }

        private async Task<ShoppingCart?> LoadCartWithDetailsAsync(Guid cartId, bool asNoTracking)
        {
            return await BuildCartQuery(asNoTracking)
                .FirstOrDefaultAsync(c => c.Id == cartId);
        }

        private async Task<ShoppingCart> ConsolidateDuplicateCartsAsync(List<ShoppingCart> carts)
        {
            var primaryCart = carts
                .OrderBy(c => c.CreatedAt)
                .First();

            foreach (var duplicateCart in carts.Where(c => c.Id != primaryCart.Id))
            {
                foreach (var duplicateItem in duplicateCart.Items.ToList())
                {
                    var existingItem = primaryCart.Items.FirstOrDefault(i =>
                        i.ProductId == duplicateItem.ProductId &&
                        i.VariantId == duplicateItem.VariantId &&
                        !i.IsDeleted);

                    if (existingItem == null)
                    {
                        duplicateCart.Items.Remove(duplicateItem);
                        duplicateItem.Cart = primaryCart;
                        duplicateItem.CartId = primaryCart.Id;
                        primaryCart.Items.Add(duplicateItem);

                        _logger.LogWarning(
                            "Moved cart item from duplicate cart. SourceCartId: {SourceCartId}, TargetCartId: {TargetCartId}, CartItemId: {CartItemId}, ProductId: {ProductId}, VariantId: {VariantId}, Quantity: {Quantity}",
                            duplicateCart.Id,
                            primaryCart.Id,
                            duplicateItem.Id,
                            duplicateItem.ProductId,
                            duplicateItem.VariantId,
                            duplicateItem.Quantity);
                    }
                    else
                    {
                        existingItem.Quantity += duplicateItem.Quantity;
                        _context.CartItems.Remove(duplicateItem);

                        _logger.LogWarning(
                            "Merged duplicate cart item into primary cart. SourceCartId: {SourceCartId}, TargetCartId: {TargetCartId}, RemovedCartItemId: {RemovedCartItemId}, ExistingCartItemId: {ExistingCartItemId}, ProductId: {ProductId}, VariantId: {VariantId}, MergedQuantity: {MergedQuantity}",
                            duplicateCart.Id,
                            primaryCart.Id,
                            duplicateItem.Id,
                            existingItem.Id,
                            duplicateItem.ProductId,
                            duplicateItem.VariantId,
                            duplicateItem.Quantity);
                    }
                }

                _context.Carts.Remove(duplicateCart);
            }

            await _context.SaveChangesAsync();

            return await LoadCartWithDetailsAsync(primaryCart.Id, asNoTracking: false)
                ?? primaryCart;
        }

        private async Task EnsureActiveStoreCustomerAsync(Guid storeCustomerId, Guid storeId)
        {
            _logger.LogInformation(
                "CartService.EnsureActiveStoreCustomerAsync started. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                storeCustomerId,
                storeId);

            var exists = await _context.StoreCustomers
                .AsNoTracking()
                .AnyAsync(c => c.Id == storeCustomerId
                            && c.StoreId == storeId
                            && c.IsActive);

            if (!exists)
            {
                _logger.LogWarning(
                    "CartService.EnsureActiveStoreCustomerAsync failed. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                    storeCustomerId,
                    storeId);
                throw new UnauthorizedAccessException("العميل لا يملك صلاحية الوصول إلى هذا المتجر");
            }

            _logger.LogInformation(
                "CartService.EnsureActiveStoreCustomerAsync succeeded. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                storeCustomerId,
                storeId);
        }

        private async Task<decimal> GetStoreCustomerDiscountPercentageAsync(
            Guid storeCustomerId,
            Guid storeId)
        {
            return await _context.StoreCustomers
                .AsNoTracking()
                .Where(customer => customer.Id == storeCustomerId
                                && customer.StoreId == storeId
                                && customer.IsActive)
                .Select(customer => customer.DiscountPercentage)
                .FirstOrDefaultAsync();
        }

        private static decimal ResolvePriceBeforeStoreCustomerDiscount(
            decimal price,
            decimal? compareAtPrice,
            bool variantPriceApplied)
        {
            if (!variantPriceApplied &&
                compareAtPrice.HasValue &&
                compareAtPrice.Value > price)
            {
                return compareAtPrice.Value;
            }

            return price;
        }
    }
}
