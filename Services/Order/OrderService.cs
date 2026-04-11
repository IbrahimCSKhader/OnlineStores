using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Order;
using onlineStore.Models.Enums;
using onlineStore.Models.Orders;
using onlineStore.Services.Pricing;
using CouponEntity = onlineStore.Models.Discounts.Coupon;

namespace onlineStore.Services.Order
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrderService> _logger;
        private readonly ICartPricingService _cartPricingService;

        public OrderService(
            AppDbContext context,
            ILogger<OrderService> logger,
            ICartPricingService cartPricingService)
        {
            _context = context;
            _logger = logger;
            _cartPricingService = cartPricingService;
        }

        public async Task<OrderDto> CreateOrderAsync(Guid storeCustomerId, CreateOrderDto dto)
        {
            _logger.LogInformation(
                "OrderService.CreateOrderAsync started. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}, CouponCode: {CouponCode}",
                storeCustomerId,
                dto?.StoreId,
                dto?.CouponCode);

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
                    _logger.LogInformation(
                        "OrderService.CreateOrderAsync customer validated. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                        storeCustomerId,
                        dto.StoreId);

                    var carts = await _context.Carts
                        .Include(c => c.Items.Where(i => !i.IsDeleted))
                            .ThenInclude(i => i.Product)
                        .Include(c => c.Items.Where(i => !i.IsDeleted))
                            .ThenInclude(i => i.Variant)
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
                            "OrderService.CreateOrderAsync detected duplicate carts during checkout. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}, CartIds: {@CartIds}",
                            storeCustomerId,
                            dto.StoreId,
                            carts.Select(c => c.Id).ToList());
                    }

                    _logger.LogInformation(
                        "OrderService.CreateOrderAsync carts loaded. CartCount: {CartCount}, CartIds: {@CartIds}, ItemsCount: {ItemsCount}",
                        carts.Count,
                        carts.Select(c => c.Id).ToList(),
                        cartItems.Count);

                    _logger.LogDebug(
                        "OrderService.CreateOrderAsync cart item snapshot. CartIds: {@CartIds}, Items: {@CartItems}",
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

                    foreach (var item in cartItems)
                    {
                        if (item.Product == null)
                            throw new Exception("يوجد عنصر غير صالح في السلة");

                        if (item.Product.StoreId != dto.StoreId)
                            throw new Exception("يوجد عنصر لا ينتمي لهذا المتجر");

                        var availableStock = item.Variant != null
                            ? item.Variant.StockQuantity
                            : item.Product.StockQuantity;

                        if (item.Product.TrackInventory && availableStock < item.Quantity)
                            throw new Exception(
                                $"الكمية المتاحة من المنتج {item.Product.Name} هي {availableStock} فقط");
                    }

                    var pricing = await _cartPricingService.CalculatePricingAsync(dto.StoreId, cartItems);
                    var subTotal = pricing.Subtotal;
                    var offerDiscount = pricing.Discount;
                    var subtotalAfterOffers = pricing.FinalTotal;
                    decimal discountAmount = offerDiscount;
                    decimal couponDiscount = 0m;
                    CouponEntity? coupon = null;

                    _logger.LogInformation(
                        "OrderService.CreateOrderAsync pricing calculated. StoreCustomerId: {StoreCustomerId}, CartId: {CartId}, SubTotal: {SubTotal}, OfferDiscount: {OfferDiscount}, SubtotalAfterOffers: {SubtotalAfterOffers}, AppliedOffers: {@AppliedOffers}",
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
                            "OrderService.CreateOrderAsync coupon applied. CouponId: {CouponId}, CouponCode: {CouponCode}, CouponDiscountAmount: {CouponDiscountAmount}, TotalDiscountAmount: {TotalDiscountAmount}",
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
                        var orderItem = new OrderItem
                        {
                            ProductId = cartItem.ProductId,
                            ProductName = cartItem.Product?.Name?.Trim() ?? "Unknown Product",
                            VariantId = cartItem.VariantId,
                            VariantName = cartItem.Variant?.Name?.Trim(),
                            Quantity = cartItem.Quantity,
                            UnitPrice = cartItem.UnitPrice,
                            TotalPrice = cartItem.UnitPrice * cartItem.Quantity,
                            CreatedAt = DateTime.UtcNow
                        };

                        order.Items.Add(orderItem);

                        if (cartItem.Product != null && cartItem.Product.TrackInventory)
                        {
                            if (cartItem.Variant != null)
                                cartItem.Variant.StockQuantity -= cartItem.Quantity;
                            else
                                cartItem.Product.StockQuantity -= cartItem.Quantity;
                        }
                    }

                    _context.Orders.Add(order);

                    if (coupon != null)
                        coupon.UsageCount += 1;

                    _context.CartItems.RemoveRange(cartItems);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation(
                        "Order created successfully: {OrderNumber} for user {UserId}. OrderId: {OrderId}, CartIds: {@CartIds}, StoreId: {StoreId}, ItemsCount: {ItemsCount}, SubTotal: {SubTotal}, OfferDiscount: {OfferDiscount}, CouponDiscount: {CouponDiscount}, TotalDiscount: {TotalDiscount}, FinalTotal: {FinalTotal}",
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

                    return createdOrder;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    _logger.LogError(
                        ex,
                        "Error while creating order for user {UserId} and store {StoreId}",
                        storeCustomerId,
                        dto.StoreId);

                    throw;
                }
            });
        }

        public async Task<List<OrderSummaryDto>> GetUserOrdersAsync(Guid storeCustomerId)
        {
            _logger.LogInformation(
                "OrderService.GetUserOrdersAsync started. StoreCustomerId: {StoreCustomerId}",
                storeCustomerId);

            var orders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.StoreCustomerId == storeCustomerId)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrderSummaryDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    Status = o.Status,
                    SubTotal = o.SubTotal,
                    DiscountAmount = o.DiscountAmount,
                    TotalAmount = o.TotalAmount,
                    ItemsCount = o.Items.Count(),
                    StoreId = o.StoreId,
                    CreatedAt = o.CreatedAt
                })
                .ToListAsync();

            _logger.LogInformation(
                "OrderService.GetUserOrdersAsync succeeded. StoreCustomerId: {StoreCustomerId}, Count: {Count}",
                storeCustomerId,
                orders.Count);

            return orders;
        }

        public async Task<OrderDto?> GetUserOrderByIdAsync(Guid storeCustomerId, Guid orderId)
        {
            _logger.LogInformation(
                "OrderService.GetUserOrderByIdAsync started. StoreCustomerId: {StoreCustomerId}, OrderId: {OrderId}",
                storeCustomerId,
                orderId);

            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Coupon)
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.StoreCustomerId == storeCustomerId && o.Id == orderId);

            _logger.LogInformation(
                "OrderService.GetUserOrderByIdAsync completed. StoreCustomerId: {StoreCustomerId}, OrderId: {OrderId}, Found: {Found}",
                storeCustomerId,
                orderId,
                order != null);
            return order == null ? null : MapOrderToDto(order);
        }

        public async Task<List<OrderSummaryDto>> GetStoreOrdersAsync(Guid storeId)
        {
            _logger.LogInformation(
                "OrderService.GetStoreOrdersAsync started. StoreId: {StoreId}",
                storeId);

            var orders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.StoreId == storeId)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrderSummaryDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    Status = o.Status,
                    SubTotal = o.SubTotal,
                    DiscountAmount = o.DiscountAmount,
                    TotalAmount = o.TotalAmount,
                    ItemsCount = o.Items.Count(),
                    StoreId = o.StoreId,
                    CreatedAt = o.CreatedAt
                })
                .ToListAsync();

            _logger.LogInformation(
                "OrderService.GetStoreOrdersAsync succeeded. StoreId: {StoreId}, Count: {Count}",
                storeId,
                orders.Count);

            return orders;
        }

        public async Task<OrderDto?> GetStoreOrderByIdAsync(Guid storeId, Guid orderId)
        {
            _logger.LogInformation(
                "OrderService.GetStoreOrderByIdAsync started. StoreId: {StoreId}, OrderId: {OrderId}",
                storeId,
                orderId);

            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Coupon)
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.StoreId == storeId && o.Id == orderId);

            _logger.LogInformation(
                "OrderService.GetStoreOrderByIdAsync completed. StoreId: {StoreId}, OrderId: {OrderId}, Found: {Found}",
                storeId,
                orderId,
                order != null);
            return order == null ? null : MapOrderToDto(order);
        }

        public async Task<OrderDto?> UpdateOrderStatusAsync(Guid orderId, UpdateOrderStatusDto dto)
        {
            _logger.LogInformation(
                "OrderService.UpdateOrderStatusAsync started. OrderId: {OrderId}, NewStatus: {Status}",
                orderId,
                dto?.Status);

            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                _logger.LogWarning(
                    "OrderService.UpdateOrderStatusAsync order not found. OrderId: {OrderId}",
                    orderId);
                return null;
            }

            order.Status = dto.Status;

            if (dto.StoreNotes != null)
                order.StoreNotes = dto.StoreNotes.Trim();

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Order status updated: {OrderId} => {Status}",
                orderId,
                dto.Status);

            return await GetOrderDtoByIdAsync(orderId);
        }

        private async Task<CouponEntity> ValidateCouponAsync(
            string code,
            Guid storeId,
            Guid storeCustomerId,
            decimal subTotal)
        {
            _logger.LogInformation(
                "OrderService.ValidateCouponAsync started. Code: {Code}, StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}, SubTotal: {SubTotal}",
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
                "OrderService.ValidateCouponAsync succeeded. CouponId: {CouponId}, CouponCode: {CouponCode}",
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
                "OrderService.GetOrderDtoByIdAsync started. OrderId: {OrderId}",
                orderId);

            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Coupon)
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            _logger.LogInformation(
                "OrderService.GetOrderDtoByIdAsync completed. OrderId: {OrderId}, Found: {Found}",
                orderId,
                order != null);
            return order == null ? null : MapOrderToDto(order);
        }

        private static OrderDto MapOrderToDto(Models.Orders.Order order)
        {
            return new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
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
                StoreId = order.StoreId,
                CouponId = order.CouponId,
                CouponCode = order.Coupon?.Code,
                CreatedAt = order.CreatedAt,
                Items = order.Items.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    VariantId = i.VariantId,
                    VariantName = i.VariantName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice
                }).ToList()
            };
        }

        private async Task EnsureActiveStoreCustomerAsync(Guid storeCustomerId, Guid storeId)
        {
            _logger.LogInformation(
                "OrderService.EnsureActiveStoreCustomerAsync started. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
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
                    "OrderService.EnsureActiveStoreCustomerAsync failed. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                    storeCustomerId,
                    storeId);
                throw new UnauthorizedAccessException("العميل لا يملك صلاحية الوصول إلى هذا المتجر");
            }

            _logger.LogInformation(
                "OrderService.EnsureActiveStoreCustomerAsync succeeded. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                storeCustomerId,
                storeId);
        }
    }
}
