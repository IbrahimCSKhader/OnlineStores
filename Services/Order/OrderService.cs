using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Order;
using onlineStore.Models;
using onlineStore.Models.CartModels;
using onlineStore.Models.Enums;
using onlineStore.Models.Orders;
using onlineStore.Security;
using onlineStore.Services.Pricing;
using CouponEntity = onlineStore.Models.Discounts.Coupon;

namespace onlineStore.Services.Order
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrderService> _logger;
        private readonly ICartPricingService _cartPricingService;
        private readonly ICurrentUserService _currentUser;

        public OrderService(
            AppDbContext context,
            ILogger<OrderService> logger,
            ICartPricingService cartPricingService,
            ICurrentUserService currentUser)
        {
            _context = context;
            _logger = logger;
            _cartPricingService = cartPricingService;
            _currentUser = currentUser;
        }

        public async Task<OrderDto> CreateOrderAsync(Guid storeCustomerId, CreateOrderDto dto)
        {
            _logger.LogInformation(
                "order=> create:start StoreCustomerId={StoreCustomerId} Request={@Request}",
                storeCustomerId,
                dto == null
                    ? null
                    : new
                    {
                        dto.StoreId,
                        dto.Title,
                        dto.CouponCode,
                        dto.CustomerNotes,
                        dto.DeliveryAddress,
                        dto.DeliveryCity,
                        dto.DeliveryPhone
                    });

            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (dto.StoreId == Guid.Empty)
                throw new Exception("StoreId is required");

            if (string.IsNullOrWhiteSpace(dto.DeliveryAddress))
                throw new Exception("عنوان التوصيل مطلوب");

            if (string.IsNullOrWhiteSpace(dto.DeliveryCity))
                throw new Exception("المدينة مطلوبة");

            if (string.IsNullOrWhiteSpace(dto.DeliveryPhone))
                throw new Exception("رقم الهاتف مطلوب");

            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    await EnsureActiveStoreCustomerAsync(storeCustomerId, dto.StoreId);
                    var customerDiscountPercentage = await GetStoreCustomerDiscountPercentageAsync(
                        storeCustomerId,
                        dto.StoreId);
                    _logger.LogInformation(
                        "order=> create:customer-validated StoreCustomerId={StoreCustomerId} StoreId={StoreId} CustomerDiscountPercentage={CustomerDiscountPercentage}",
                        storeCustomerId,
                        dto.StoreId,
                        customerDiscountPercentage);

                    var carts = await _context.Carts
                        .Include(c => c.Items.Where(i => !i.IsDeleted))
                            .ThenInclude(i => i.Product)
                                .ThenInclude(p => p.Variants.Where(v => !v.IsDeleted && v.IsActive))
                        .Include(c => c.Items.Where(i => !i.IsDeleted))
                            .ThenInclude(i => i.Product)
                        .Include(c => c.Items.Where(i => !i.IsDeleted))
                            .ThenInclude(i => i.Variant)
                        .AsSplitQuery()
                        .Where(c =>
                            c.StoreCustomerId == storeCustomerId &&
                            c.StoreId == dto.StoreId)
                        .OrderBy(c => c.CreatedAt)
                        .ToListAsync();

                    var cartItems = carts
                        .SelectMany(c => c.Items)
                        .ToList();

                    if (!cartItems.Any())
                        throw new Exception("السلة فارغة");

                    if (carts.Count > 1)
                    {
                        _logger.LogWarning(
                            "order=> create:duplicate-carts StoreCustomerId={StoreCustomerId} StoreId={StoreId} CartIds={@CartIds}",
                            storeCustomerId,
                            dto.StoreId,
                            carts.Select(c => c.Id).ToList());
                    }

                    _logger.LogInformation(
                        "order=> create:carts-loaded StoreCustomerId={StoreCustomerId} StoreId={StoreId} CartCount={CartCount} CartIds={@CartIds} ItemsCount={ItemsCount}",
                        storeCustomerId,
                        dto.StoreId,
                        carts.Count,
                        carts.Select(c => c.Id).ToList(),
                        cartItems.Count);

                    _logger.LogDebug(
                        "order=> create:cart-items CartIds={@CartIds} Items={@CartItems}",
                        carts.Select(c => c.Id).ToList(),
                        cartItems.Select(item => new
                        {
                            item.Id,
                            item.CartId,
                            item.ProductId,
                            ProductName = item.Product?.Name,
                            item.VariantId,
                            VariantName = item.Variant?.Name,
                            item.Quantity,
                            item.UnitPrice
                        }).ToList());

                    var resolvedCartItems = new Dictionary<Guid, ResolvedOrderCartItem>();

                    foreach (var item in cartItems)
                    {
                        if (item.Product == null)
                            throw new Exception("يوجد عنصر غير صالح في السلة");

                        if (item.Product.StoreId != dto.StoreId)
                            throw new Exception("يوجد عنصر لا ينتمي لهذا المتجر");

                        var resolvedVariant = ResolveVariantForOrderCartItem(item);
                        var availableStock = resolvedVariant != null
                            ? resolvedVariant.StockQuantity
                            : item.Product.StockQuantity;

                        if (item.Product.TrackInventory && availableStock < item.Quantity)
                            throw new Exception(
                                $"الكمية المتاحة من المنتج {item.Product.Name} هي {availableStock} فقط");

                        var unitPrice = ResolveOrderCartItemUnitPrice(
                            item.Product,
                            resolvedVariant,
                            customerDiscountPercentage);

                        item.UnitPrice = unitPrice;
                        resolvedCartItems[item.Id] = new ResolvedOrderCartItem(
                            resolvedVariant,
                            unitPrice);
                    }

                    var pricing = await _cartPricingService.CalculatePricingAsync(
                        dto.StoreId,
                        cartItems,
                        customerDiscountPercentage);
                    var subTotal = pricing.Subtotal;
                    var offerDiscount = pricing.Discount;
                    var subtotalAfterOffers = pricing.FinalTotal;
                    decimal discountAmount = offerDiscount;
                    decimal couponDiscount = 0m;
                    CouponEntity? coupon = null;

                    _logger.LogInformation(
                        "order=> create:pricing-calculated StoreCustomerId={StoreCustomerId} CartId={CartId} SubTotal={SubTotal} OfferDiscount={OfferDiscount} SubtotalAfterOffers={SubtotalAfterOffers} AppliedOffers={@AppliedOffers}",
                        storeCustomerId,
                        carts.First().Id,
                        subTotal,
                        offerDiscount,
                        subtotalAfterOffers,
                        pricing.AppliedOffers);

                    if (!string.IsNullOrWhiteSpace(dto.CouponCode))
                    {
                        coupon = await ValidateCouponAsync(
                            dto.CouponCode.Trim(),
                            dto.StoreId,
                            storeCustomerId,
                            subtotalAfterOffers);

                        couponDiscount = CalculateDiscount(coupon, subtotalAfterOffers);
                        discountAmount += couponDiscount;
                        _logger.LogInformation(
                            "order=> create:coupon-applied CouponId={CouponId} CouponCode={CouponCode} CouponDiscountAmount={CouponDiscountAmount} TotalDiscountAmount={TotalDiscountAmount}",
                            coupon.Id,
                            coupon.Code,
                            couponDiscount,
                            discountAmount);
                    }

                    var totalAmount = subtotalAfterOffers - couponDiscount;
                    if (totalAmount < 0)
                        totalAmount = 0;

                    var order = new Models.Orders.Order
                    {
                        OrderNumber = GenerateOrderNumber(),
                        Title = BuildOrderTitle(dto.Title, cartItems),
                        Status = OrderStatus.Pending,
                        SubTotal = subTotal,
                        DiscountAmount = discountAmount,
                        TotalAmount = totalAmount,
                        CustomerNotes = dto.CustomerNotes?.Trim(),
                        DeliveryAddress = dto.DeliveryAddress.Trim(),
                        DeliveryCity = dto.DeliveryCity.Trim(),
                        DeliveryPhone = dto.DeliveryPhone.Trim(),
                        CouponId = coupon?.Id,
                        StoreCustomerId = storeCustomerId,
                        StoreId = dto.StoreId,
                        CreatedAt = DateTime.UtcNow,
                        Items = new List<OrderItem>()
                    };

                    foreach (var cartItem in cartItems)
                    {
                        var resolvedCartItem = resolvedCartItems[cartItem.Id];
                        var orderItem = new OrderItem
                        {
                            ProductId = cartItem.ProductId,
                            ProductName = cartItem.Product?.Name?.Trim() ?? "Unknown Product",
                            VariantId = resolvedCartItem.Variant?.Id,
                            VariantName = resolvedCartItem.Variant?.Name?.Trim(),
                            Quantity = cartItem.Quantity,
                            UnitPrice = resolvedCartItem.UnitPrice,
                            TotalPrice = resolvedCartItem.UnitPrice * cartItem.Quantity,
                            CreatedAt = DateTime.UtcNow
                        };

                        order.Items.Add(orderItem);
                    }

                    _context.Orders.Add(order);

                    if (coupon != null)
                        coupon.UsageCount += 1;

                    _context.CartItems.RemoveRange(cartItems);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation(
                        "order=> create:saved OrderNumber={OrderNumber} UserId={UserId} OrderId={OrderId} CartIds={@CartIds} StoreId={StoreId} ItemsCount={ItemsCount} SubTotal={SubTotal} OfferDiscount={OfferDiscount} CouponDiscount={CouponDiscount} TotalDiscount={TotalDiscount} FinalTotal={FinalTotal}",
                        order.OrderNumber,
                        storeCustomerId,
                        order.Id,
                        carts.Select(c => c.Id).ToList(),
                        dto.StoreId,
                        order.Items.Count,
                        subTotal,
                        offerDiscount,
                        couponDiscount,
                        discountAmount,
                        totalAmount);

                    var createdOrder = await GetOrderDtoByIdAsync(order.Id);
                    if (createdOrder == null)
                        throw new Exception("فشل في تحميل الطلب بعد إنشائه");

                    _logger.LogInformation(
                        "order=> create:result Order={@Order}",
                        ToOrderLogModel(createdOrder));

                    return createdOrder;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    _logger.LogError(
                        ex,
                        "order=> create:error UserId={UserId} StoreId={StoreId}",
                        storeCustomerId,
                        dto.StoreId);

                    throw;
                }
            });
        }

        public async Task<List<OrderSummaryDto>> GetUserOrdersAsync(Guid storeCustomerId)
        {
            _logger.LogInformation(
                "order=> get-user-orders:start StoreCustomerId={StoreCustomerId}",
                storeCustomerId);

            var orders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.StoreCustomerId == storeCustomerId)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrderSummaryDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    Title = o.Title ?? o.Items
                        .Select(i => i.ProductName)
                        .FirstOrDefault(),
                    Status = o.Status,
                    SubTotal = o.SubTotal,
                    DiscountAmount = o.DiscountAmount,
                    TotalAmount = o.TotalAmount,
                    StoreCustomerId = o.StoreCustomerId,
                    CustomerName = (o.StoreCustomer.FirstName + " " + o.StoreCustomer.LastName).Trim(),
                    CustomerEmail = o.StoreCustomer.Email,
                    CustomerPhone = o.StoreCustomer.Phone,
                    CustomerDiscountPercentage = o.StoreCustomer.DiscountPercentage,
                    ItemsCount = o.Items.Count(),
                    StoreId = o.StoreId,
                    CouponId = o.CouponId,
                    CouponCode = o.Coupon != null ? o.Coupon.Code : null,
                    CouponDiscountType = o.Coupon != null ? o.Coupon.DiscountType : null,
                    CouponDiscountValue = o.Coupon != null ? o.Coupon.DiscountValue : null,
                    CreatedAt = o.CreatedAt
                })
                .ToListAsync();

            _logger.LogInformation(
                "order=> get-user-orders:result StoreCustomerId={StoreCustomerId} Count={Count} Orders={@Orders}",
                storeCustomerId,
                orders.Count,
                orders.Select(ToOrderSummaryLogModel).ToList());

            return orders;
        }

        public async Task<OrderDto?> GetUserOrderByIdAsync(Guid storeCustomerId, Guid orderId)
        {
            _logger.LogInformation(
                "order=> get-user-order-by-id:start StoreCustomerId={StoreCustomerId} OrderId={OrderId}",
                storeCustomerId,
                orderId);

            var order = await BuildOrderDetailsQuery(asNoTracking: true)
                .FirstOrDefaultAsync(o => o.StoreCustomerId == storeCustomerId && o.Id == orderId);

            _logger.LogInformation(
                "order=> get-user-order-by-id:result StoreCustomerId={StoreCustomerId} OrderId={OrderId} Found={Found} Order={@Order}",
                storeCustomerId,
                orderId,
                order != null,
                order == null ? null : ToOrderLogModel(MapOrderToDto(order)));
            return order == null ? null : MapOrderToDto(order);
        }

        public async Task<List<OrderSummaryDto>> GetStoreOrdersAsync(Guid storeId)
        {
            _logger.LogInformation(
                "order=> get-store-orders:start StoreId={StoreId}",
                storeId);

            await EnsureCanAccessStoreOrdersAsync(storeId);

            var orders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.StoreId == storeId)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrderSummaryDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    Title = o.Title ?? o.Items
                        .Select(i => i.ProductName)
                        .FirstOrDefault(),
                    Status = o.Status,
                    SubTotal = o.SubTotal,
                    DiscountAmount = o.DiscountAmount,
                    TotalAmount = o.TotalAmount,
                    StoreCustomerId = o.StoreCustomerId,
                    CustomerName = (o.StoreCustomer.FirstName + " " + o.StoreCustomer.LastName).Trim(),
                    CustomerEmail = o.StoreCustomer.Email,
                    CustomerPhone = o.StoreCustomer.Phone,
                    CustomerDiscountPercentage = o.StoreCustomer.DiscountPercentage,
                    ItemsCount = o.Items.Count(),
                    StoreId = o.StoreId,
                    CouponId = o.CouponId,
                    CouponCode = o.Coupon != null ? o.Coupon.Code : null,
                    CouponDiscountType = o.Coupon != null ? o.Coupon.DiscountType : null,
                    CouponDiscountValue = o.Coupon != null ? o.Coupon.DiscountValue : null,
                    CreatedAt = o.CreatedAt
                })
                .ToListAsync();

            _logger.LogInformation(
                "order=> get-store-orders:result StoreId={StoreId} Count={Count} Orders={@Orders}",
                storeId,
                orders.Count,
                orders.Select(ToOrderSummaryLogModel).ToList());

            return orders;
        }

        public async Task<OrderDto?> GetStoreOrderByIdAsync(Guid storeId, Guid orderId)
        {
            _logger.LogInformation(
                "order=> get-store-order-by-id:start StoreId={StoreId} OrderId={OrderId}",
                storeId,
                orderId);

            await EnsureCanAccessStoreOrdersAsync(storeId);

            var order = await BuildOrderDetailsQuery(asNoTracking: true)
                .FirstOrDefaultAsync(o => o.StoreId == storeId && o.Id == orderId);

            _logger.LogInformation(
                "order=> get-store-order-by-id:result StoreId={StoreId} OrderId={OrderId} Found={Found} Order={@Order}",
                storeId,
                orderId,
                order != null,
                order == null ? null : ToOrderLogModel(MapOrderToDto(order)));
            return order == null ? null : MapOrderToDto(order);
        }

        public async Task<OrderDto?> UpdateOrderStatusAsync(Guid orderId, UpdateOrderStatusDto dto)
        {
            _logger.LogInformation(
                "order=> update-status:start OrderId={OrderId} NewStatus={Status} StoreNotes={StoreNotes}",
                orderId,
                dto?.Status,
                dto?.StoreNotes);

            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var order = await _context.Orders
                        .Include(o => o.Items)
                            .ThenInclude(i => i.Product)
                                .ThenInclude(p => p.Variants.Where(v => !v.IsDeleted && v.IsActive))
                        .Include(o => o.Items)
                            .ThenInclude(i => i.Product)
                        .Include(o => o.Items)
                            .ThenInclude(i => i.Variant)
                                .ThenInclude(v => v!.Images)
                        .FirstOrDefaultAsync(o => o.Id == orderId);

                    if (order == null)
                    {
                        _logger.LogWarning(
                            "order=> update-status:not-found OrderId={OrderId}",
                            orderId);
                        return null;
                    }

                    await EnsureCanAccessStoreOrdersAsync(order.StoreId);

                    var previousStatus = order.Status;
                    ApplyInventoryForStatusTransition(order, dto.Status);
                    order.Status = dto.Status;

                    if (dto.StoreNotes != null)
                        order.StoreNotes = dto.StoreNotes.Trim();

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation(
                        "order=> update-status:saved OrderId={OrderId} PreviousStatus={PreviousStatus} Status={Status}",
                        orderId,
                        previousStatus,
                        dto.Status);

                    return await GetOrderDtoByIdAsync(orderId);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    _logger.LogError(
                        ex,
                        "order=> update-status:error OrderId={OrderId} NewStatus={Status}",
                        orderId,
                        dto.Status);

                    throw;
                }
            });
        }

        private void ApplyInventoryForStatusTransition(
            Models.Orders.Order order,
            OrderStatus nextStatus)
        {
            var previousStatus = order.Status;

            if (previousStatus == nextStatus)
                return;

            var hadInventoryReserved = IsInventoryReservedStatus(previousStatus);
            var shouldReserveInventory = IsInventoryReservedStatus(nextStatus);

            if (!hadInventoryReserved && shouldReserveInventory)
            {
                ReserveInventoryForConfirmedOrder(order);
                return;
            }

            if (hadInventoryReserved && !shouldReserveInventory)
            {
                RestoreInventoryForCancelledOrder(order);
            }
        }

        private static bool IsInventoryReservedStatus(OrderStatus status)
        {
            return status != OrderStatus.Pending &&
                status != OrderStatus.Cancelled;
        }

        private void RestoreInventoryForCancelledOrder(Models.Orders.Order order)
        {
            foreach (var item in order.Items)
            {
                if (item.Product == null)
                {
                    _logger.LogWarning(
                        "order=> update-status:restore-skip-missing-product OrderId={OrderId} OrderItemId={OrderItemId} ProductId={ProductId}",
                        order.Id,
                        item.Id,
                        item.ProductId);
                    continue;
                }

                if (!item.Product.TrackInventory)
                    continue;

                if (item.VariantId.HasValue)
                {
                    if (item.Variant == null)
                    {
                        _logger.LogWarning(
                            "order=> update-status:restore-skip-missing-variant OrderId={OrderId} OrderItemId={OrderItemId} VariantId={VariantId}",
                            order.Id,
                            item.Id,
                            item.VariantId);
                        continue;
                    }

                    item.Variant.StockQuantity += item.Quantity;
                    continue;
                }

                item.Product.StockQuantity += item.Quantity;
            }
        }

        private void ReserveInventoryForConfirmedOrder(Models.Orders.Order order)
        {
            foreach (var item in order.Items)
            {
                if (item.Product == null)
                {
                    throw new InvalidOperationException(
                        $"لا يمكن تأكيد الطلب لأن المنتج {BuildInventoryItemLabel(item)} لم يعد موجودًا.");
                }

                if (!item.Product.TrackInventory)
                    continue;

                var inventoryVariant = ResolveInventoryVariantForOrderItem(item);
                if (inventoryVariant != null)
                {
                    if (inventoryVariant.StockQuantity < item.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"لا يمكن تأكيد الطلب لأن الكمية المتاحة من {BuildInventoryItemLabel(item)} هي {inventoryVariant.StockQuantity} فقط.");
                    }

                    if (!item.VariantId.HasValue)
                    {
                        item.VariantId = inventoryVariant.Id;
                        item.VariantName ??= inventoryVariant.Name?.Trim();
                        item.Variant = inventoryVariant;
                    }

                    inventoryVariant.StockQuantity -= item.Quantity;
                    continue;
                }

                if (item.Product.StockQuantity < item.Quantity)
                {
                    throw new InvalidOperationException(
                        $"لا يمكن تأكيد الطلب لأن الكمية المتاحة من {BuildInventoryItemLabel(item)} هي {item.Product.StockQuantity} فقط.");
                }

                item.Product.StockQuantity -= item.Quantity;
            }
        }

        private static ProductVariant? ResolveInventoryVariantForOrderItem(OrderItem item)
        {
            if (item.VariantId.HasValue)
            {
                var variant = item.Variant
                    ?? item.Product?.Variants.FirstOrDefault(v => v.Id == item.VariantId.Value);

                if (variant == null)
                {
                    throw new InvalidOperationException(
                        $"لا يمكن تأكيد الطلب لأن المتغير {BuildInventoryItemLabel(item)} لم يعد موجودًا.");
                }

                if (variant.ProductId != item.ProductId)
                {
                    throw new InvalidOperationException(
                        $"لا يمكن تأكيد الطلب لأن المتغير {BuildInventoryItemLabel(item)} لا يتبع المنتج.");
                }

                if (variant.IsDeleted || !variant.IsActive)
                {
                    throw new InvalidOperationException(
                        $"لا يمكن تأكيد الطلب لأن المتغير {BuildInventoryItemLabel(item)} غير متاح حالياً.");
                }

                return variant;
            }

            return item.Product == null
                ? null
                : ResolveDefaultActiveVariant(item.Product);
        }

        private static string BuildInventoryItemLabel(OrderItem item)
        {
            var productName = string.IsNullOrWhiteSpace(item.ProductName)
                ? "هذا المنتج"
                : item.ProductName.Trim();
            var variantName = item.VariantName?.Trim();

            return string.IsNullOrWhiteSpace(variantName)
                ? productName
                : $"{productName} - {variantName}";
        }

        private static ProductVariant? ResolveVariantForOrderCartItem(CartItem item)
        {
            if (item.VariantId.HasValue)
            {
                var variant = item.Variant
                    ?? item.Product?.Variants.FirstOrDefault(v => v.Id == item.VariantId.Value);

                if (variant == null)
                {
                    throw new Exception("النسخة المرتبطة بالسلة غير موجودة");
                }

                if (variant.ProductId != item.ProductId)
                {
                    throw new Exception("النسخة المرتبطة بالسلة لا تتبع المنتج");
                }

                if (variant.IsDeleted || !variant.IsActive)
                {
                    throw new Exception("النسخة المرتبطة بالسلة غير متاحة حالياً");
                }

                return variant;
            }

            return ResolveDefaultActiveVariant(item.Product);
        }

        private static ProductVariant? ResolveDefaultActiveVariant(onlineStore.Models.Product product)
        {
            return product.Variants?
                .Where(v => v.IsActive && !v.IsDeleted)
                .OrderByDescending(v => v.IsDefault)
                .ThenBy(v => v.SortOrder)
                .ThenBy(v => v.CreatedAt)
                .FirstOrDefault();
        }

        private static decimal ResolveOrderCartItemUnitPrice(
            onlineStore.Models.Product product,
            ProductVariant? variant,
            decimal customerDiscountPercentage)
        {
            var price = variant?.Price ?? product.Price;

            if (customerDiscountPercentage <= 0m)
            {
                return price;
            }

            var compareAtPrice = variant?.CompareAtPrice ?? product.CompareAtPrice;
            var variantPriceApplied = variant?.Price.HasValue == true;
            var priceBeforeDiscount = ResolvePriceBeforeStoreCustomerDiscount(
                price,
                compareAtPrice,
                variantPriceApplied);

            return priceBeforeDiscount - (priceBeforeDiscount * customerDiscountPercentage / 100m);
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

        private async Task<CouponEntity> ValidateCouponAsync(
            string code,
            Guid storeId,
            Guid storeCustomerId,
            decimal subTotal)
        {
            _logger.LogInformation(
                "order=> validate-coupon:start Code={Code} StoreId={StoreId} StoreCustomerId={StoreCustomerId} SubTotal={SubTotal}",
                code,
                storeId,
                storeCustomerId,
                subTotal);

            var normalizedCode = code.Trim().ToUpper();

            var coupon = await _context.Coupons
                .FirstOrDefaultAsync(c =>
                    c.Code == normalizedCode &&
                    c.StoreId == storeId &&
                    c.IsActive);

            if (coupon == null)
                throw new Exception("الكوبون غير موجود أو غير فعال");

            var now = DateTime.UtcNow;

            if (coupon.StartsAt.HasValue && coupon.StartsAt.Value > now)
                throw new Exception("الكوبون غير متاح بعد");

            if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt.Value < now)
                throw new Exception("الكوبون منتهي الصلاحية");

            if (coupon.MinOrderAmount.HasValue && subTotal < coupon.MinOrderAmount.Value)
                throw new Exception(
                    $"الحد الأدنى لاستخدام الكوبون هو {coupon.MinOrderAmount.Value}");

            if (coupon.UsageLimit.HasValue && coupon.UsageCount >= coupon.UsageLimit.Value)
                throw new Exception("تم الوصول للحد الأقصى لاستخدام الكوبون");

            if (coupon.PerUserLimit.HasValue)
            {
                var userUsageCount = await _context.Orders
                    .CountAsync(o => o.StoreCustomerId == storeCustomerId && o.CouponId == coupon.Id);

                if (userUsageCount >= coupon.PerUserLimit.Value)
                    throw new Exception("تم استخدام هذا الكوبون من قبلك مسبقاً");
            }

            _logger.LogInformation(
                "order=> validate-coupon:result CouponId={CouponId} CouponCode={CouponCode}",
                coupon.Id,
                coupon.Code);
            return coupon;
        }

        private static decimal CalculateDiscount(CouponEntity coupon, decimal subTotal)
        {
            decimal discount = 0m;

            if (coupon.DiscountType == DiscountType.Percentage)
            {
                discount = subTotal * (coupon.DiscountValue / 100m);

                if (coupon.MaxDiscountAmount.HasValue &&
                    discount > coupon.MaxDiscountAmount.Value)
                {
                    discount = coupon.MaxDiscountAmount.Value;
                }
            }
            else if (coupon.DiscountType == DiscountType.FixedAmount)
            {
                discount = coupon.DiscountValue;
            }

            if (discount > subTotal)
                discount = subTotal;

            return discount;
        }

        private static string GenerateOrderNumber()
        {
            return $"ORD-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Random.Shared.Next(100, 999)}";
        }

        private async Task<OrderDto?> GetOrderDtoByIdAsync(Guid orderId)
        {
            _logger.LogInformation(
                "order=> get-order-dto-by-id:start OrderId={OrderId}",
                orderId);

            var order = await BuildOrderDetailsQuery(asNoTracking: true)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            _logger.LogInformation(
                "order=> get-order-dto-by-id:result OrderId={OrderId} Found={Found} Order={@Order}",
                orderId,
                order != null,
                order == null ? null : ToOrderLogModel(MapOrderToDto(order)));
            return order == null ? null : MapOrderToDto(order);
        }

        private IQueryable<Models.Orders.Order> BuildOrderDetailsQuery(bool asNoTracking)
        {
            var query = _context.Orders
                .Include(o => o.Coupon)
                .Include(o => o.StoreCustomer)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p.Images)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Variant)
                        .ThenInclude(v => v!.Images)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Variant)
                        .ThenInclude(v => v!.AttributeValues)
                            .ThenInclude(vav => vav.AttributeValue)
                                .ThenInclude(av => av.Attribute)
                .AsSplitQuery();

            return asNoTracking ? query.AsNoTracking() : query;
        }

        private static string? ResolveProductImageUrl(onlineStore.Models.Product? product)
        {
            return product?.ThumbnailUrl
                ?? product?.Images?
                    .Where(i => i.VariantId == null && !i.IsDeleted)
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => i.Url)
                    .FirstOrDefault();
        }

        private static string? ResolveEffectiveVariantImageUrl(
            ProductVariant? variant,
            onlineStore.Models.Product? product)
        {
            var variantImageUrl = variant?.Images?
                .Where(i => !i.IsDeleted)
                .OrderBy(i => i.DisplayOrder)
                .Select(i => i.Url)
                .FirstOrDefault();

            return variant?.ImageUrl
                ?? variantImageUrl
                ?? ResolveProductImageUrl(product);
        }

        private static List<OrderItemVariantAttributeDto> MapOrderVariantAttributes(
            ProductVariant? variant)
        {
            return variant?.AttributeValues?
                .Where(vav => !vav.IsDeleted)
                .OrderBy(vav => vav.AttributeValue.Attribute != null
                    ? vav.AttributeValue.Attribute.Name
                    : string.Empty)
                .ThenBy(vav => vav.AttributeValue.Value)
                .Select(vav => new OrderItemVariantAttributeDto
                {
                    AttributeValueId = vav.AttributeValueId,
                    AttributeId = vav.AttributeValue.AttributeId,
                    AttributeName = vav.AttributeValue.Attribute?.Name ?? string.Empty,
                    Value = vav.AttributeValue.Value
                })
                .ToList() ?? new List<OrderItemVariantAttributeDto>();
        }

        private static OrderDto MapOrderToDto(Models.Orders.Order order)
        {
            return new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                Title = ResolveOrderTitle(order.Title, order.Items),
                Status = order.Status,
                SubTotal = order.SubTotal,
                DiscountAmount = order.DiscountAmount,
                TotalAmount = order.TotalAmount,
                CustomerNotes = order.CustomerNotes,
                StoreNotes = order.StoreNotes,
                DeliveryAddress = order.DeliveryAddress,
                DeliveryCity = order.DeliveryCity,
                DeliveryPhone = order.DeliveryPhone,
                StoreCustomerId = order.StoreCustomerId,
                CustomerName = BuildCustomerFullName(order.StoreCustomer),
                CustomerEmail = order.StoreCustomer?.Email,
                CustomerPhone = order.StoreCustomer?.Phone,
                StoreId = order.StoreId,
                CouponId = order.CouponId,
                CouponCode = order.Coupon?.Code,
                CouponDiscountType = order.Coupon?.DiscountType,
                CouponDiscountValue = order.Coupon?.DiscountValue,
                CustomerDiscountPercentage = order.StoreCustomer?.DiscountPercentage ?? 0m,
                CreatedAt = order.CreatedAt,
                Items = order.Items.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    VariantId = i.VariantId,
                    VariantName = i.VariantName ?? i.Variant?.Name,
                    VariantSKU = i.Variant?.SKU,
                    VariantImageUrl = i.Variant?.ImageUrl,
                    EffectiveVariantImageUrl = ResolveEffectiveVariantImageUrl(i.Variant, i.Product),
                    VariantAttributes = MapOrderVariantAttributes(i.Variant),
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice
                }).ToList()
            };
        }

        private async Task EnsureActiveStoreCustomerAsync(Guid storeCustomerId, Guid storeId)
        {
            _logger.LogInformation(
                "order=> ensure-active-customer:start StoreCustomerId={StoreCustomerId} StoreId={StoreId}",
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
                    "order=> ensure-active-customer:failed StoreCustomerId={StoreCustomerId} StoreId={StoreId}",
                    storeCustomerId,
                    storeId);
                throw new UnauthorizedAccessException("العميل لا يملك صلاحية الوصول إلى هذا المتجر");
            }

            _logger.LogInformation(
                "order=> ensure-active-customer:result StoreCustomerId={StoreCustomerId} StoreId={StoreId}",
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

        private async Task EnsureCanAccessStoreOrdersAsync(Guid storeId)
        {
            if (_currentUser.IsSuperAdmin)
                return;

            if (!_currentUser.IsStoreOwner || !_currentUser.UserId.HasValue)
            {
                _logger.LogWarning(
                    "order=> access:forbidden UserId={UserId} StoreId={StoreId}",
                    _currentUser.UserId,
                    storeId);

                throw new UnauthorizedAccessException("Only super admins and the store owner can access store orders.");
            }

            var ownsStore = await _context.Stores
                .AsNoTracking()
                .AnyAsync(store => store.Id == storeId && store.OwnerId == _currentUser.UserId.Value);

            if (ownsStore)
                return;

            _logger.LogWarning(
                "order=> access:wrong-owner UserId={UserId} StoreId={StoreId}",
                _currentUser.UserId,
                storeId);

            throw new UnauthorizedAccessException("Only super admins and the store owner can access store orders.");
        }

        private static string? BuildOrderTitle(
            string? requestedTitle,
            IReadOnlyCollection<onlineStore.Models.CartModels.CartItem> cartItems)
        {
            var normalizedTitle = requestedTitle?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedTitle))
                return normalizedTitle;

            var itemNames = cartItems
                .Select(item => item.Product?.Name?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return BuildFallbackTitleFromNames(itemNames, cartItems.Count);
        }

        private static string? ResolveOrderTitle(
            string? persistedTitle,
            IEnumerable<OrderItem> items)
        {
            var normalizedTitle = persistedTitle?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedTitle))
                return normalizedTitle;

            var materializedItems = items.ToList();

            var itemNames = materializedItems
                .Select(item => item.ProductName?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return BuildFallbackTitleFromNames(itemNames, materializedItems.Count);
        }

        private static string? BuildFallbackTitleFromNames(
            IReadOnlyList<string> itemNames,
            int itemCount)
        {
            if (itemNames.Count == 0)
                return null;

            if (itemNames.Count == 1)
                return itemNames[0];

            return $"{itemNames[0]} + {Math.Max(itemCount - 1, 1)} more";
        }

        private static string? BuildCustomerFullName(StoreCustomer? customer)
        {
            if (customer == null)
                return null;

            var fullName = $"{customer.FirstName} {customer.LastName}".Trim();
            return string.IsNullOrWhiteSpace(fullName) ? customer.Email : fullName;
        }

        private static object ToOrderLogModel(OrderDto order)
        {
            return new
            {
                order.Id,
                order.OrderNumber,
                order.Title,
                order.Status,
                order.SubTotal,
                order.DiscountAmount,
                order.TotalAmount,
                order.StoreCustomerId,
                order.StoreId,
                order.CouponId,
                order.CouponCode,
                order.CreatedAt,
                ItemsCount = order.Items.Count,
                Items = order.Items.Select(item => new
                {
                    item.Id,
                    item.ProductId,
                    item.ProductName,
                    item.VariantId,
                    item.VariantName,
                    item.Quantity,
                    item.UnitPrice,
                    item.TotalPrice
                }).ToList()
            };
        }

        private static object ToOrderSummaryLogModel(OrderSummaryDto order)
        {
            return new
            {
                order.Id,
                order.OrderNumber,
                order.Title,
                order.Status,
                order.SubTotal,
                order.DiscountAmount,
                order.TotalAmount,
                order.ItemsCount,
                order.StoreId,
                order.CreatedAt
            };
        }

        private sealed record ResolvedOrderCartItem(
            ProductVariant? Variant,
            decimal UnitPrice);
    }
}
