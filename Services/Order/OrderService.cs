using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Order;
using onlineStore.Models.Enums;
using onlineStore.Models.Orders;
using CouponEntity = onlineStore.Models.Discounts.Coupon;

namespace onlineStore.Services.Order
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            AppDbContext context,
            ILogger<OrderService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<OrderDto> CreateOrderAsync(Guid userId, CreateOrderDto dto)
        {
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
                    var cart = await _context.Carts
                        .Include(c => c.Items)
                            .ThenInclude(i => i.Product)
                        .Include(c => c.Items)
                            .ThenInclude(i => i.Variant)
                        .FirstOrDefaultAsync(c =>
                            c.UserId == userId &&
                            c.StoreId == dto.StoreId);

                    if (cart == null || cart.Items == null || !cart.Items.Any())
                        throw new Exception("السلة فارغة");

                    foreach (var item in cart.Items)
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

                    var subTotal = cart.Items.Sum(i => i.UnitPrice * i.Quantity);
                    decimal discountAmount = 0m;
                    CouponEntity? coupon = null;

                    if (!string.IsNullOrWhiteSpace(dto.CouponCode))
                    {
                        coupon = await ValidateCouponAsync(
                            dto.CouponCode.Trim(),
                            dto.StoreId,
                            userId,
                            subTotal);

                        discountAmount = CalculateDiscount(coupon, subTotal);
                    }

                    var totalAmount = subTotal - discountAmount;
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
                        UserId = userId,
                        StoreId = dto.StoreId,
                        CreatedAt = DateTime.UtcNow,
                        Items = new List<OrderItem>()
                    };

                    foreach (var cartItem in cart.Items)
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

                    _context.CartItems.RemoveRange(cart.Items);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation(
                        "Order created successfully: {OrderNumber} for user {UserId}",
                        order.OrderNumber,
                        userId);

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
                        userId,
                        dto.StoreId);

                    throw;
                }
            });
        }

        public async Task<List<OrderSummaryDto>> GetUserOrdersAsync(Guid userId)
        {
            return await _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId == userId)
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
        }

        public async Task<OrderDto?> GetUserOrderByIdAsync(Guid userId, Guid orderId)
        {
            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Coupon)
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.UserId == userId && o.Id == orderId);

            return order == null ? null : MapOrderToDto(order);
        }

        public async Task<List<OrderSummaryDto>> GetStoreOrdersAsync(Guid storeId)
        {
            return await _context.Orders
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
        }

        public async Task<OrderDto?> GetStoreOrderByIdAsync(Guid storeId, Guid orderId)
        {
            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Coupon)
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.StoreId == storeId && o.Id == orderId);

            return order == null ? null : MapOrderToDto(order);
        }

        public async Task<OrderDto?> UpdateOrderStatusAsync(Guid orderId, UpdateOrderStatusDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return null;

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
            Guid userId,
            decimal subTotal)
        {
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
                    .CountAsync(o => o.UserId == userId && o.CouponId == coupon.Id);

                if (userUsageCount >= coupon.PerUserLimit.Value)
                    throw new Exception("تم استخدام هذا الكوبون من قبلك مسبقاً");
            }

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
            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Coupon)
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

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
                UserId = order.UserId,
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
    }
}