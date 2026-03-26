using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Order;
using CouponEntity = onlineStore.Models.Discounts.Coupon;
using onlineStore.Models.Enums;
using onlineStore.Models.Orders;
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
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                        .ThenInclude(i => i.Product)
                    .Include(c => c.Items)
                        .ThenInclude(i => i.Variant)
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.StoreId == dto.StoreId);

                if (cart == null || cart.Items == null || !cart.Items.Any())
                    throw new Exception("Empty cart");

                foreach (var item in cart.Items)
                {
                    if (item.Product == null)
                        throw new Exception("their ar an unvalid item");

                    if (item.Product.StoreId != dto.StoreId)
                        throw new Exception("their are an item didnt belong to the store");

                    var availableStock = item.Variant != null
                        ? item.Variant.StockQuantity
                        : item.Product.StockQuantity;

                    if (item.Product.TrackInventory && availableStock < item.Quantity)
                        throw new Exception($"the current quantity for item {item.Product.Name} is {availableStock} only");
                }

                var subTotal = cart.Items.Sum(i => i.UnitPrice * i.Quantity);
                var discountAmount = 0m;
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
                    OrderNumber = await GenerateOrderNumberAsync(),
                    Status = OrderStatus.Pending,
                    SubTotal = subTotal,
                    DiscountAmount = discountAmount,
                    TotalAmount = totalAmount,
                    CustomerNotes = dto.CustomerNotes?.Trim(),
                    DeliveryAddress = dto.DeliveryAddress?.Trim(),
                    DeliveryCity = dto.DeliveryCity?.Trim(),
                    DeliveryPhone = dto.DeliveryPhone?.Trim(),
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
                    "Order created: {OrderNumber} for user {UserId}",
                    order.OrderNumber, userId);

                return await GetOrderDtoByIdAsync(order.Id)
                       ?? throw new Exception("error in downloading");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _logger.LogError(ex,
                    "Error while creating order for user {UserId} and store {StoreId}",
                    userId, dto.StoreId);

                throw;
            }
        }

        // ════════════════════════════════════════════════════
        // Get User Orders
        // ════════════════════════════════════════════════════
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
                    ItemsCount = o.Items.Count,
                    StoreId = o.StoreId,
                    CreatedAt = o.CreatedAt
                })
                .ToListAsync();
        }

        // ════════════════════════════════════════════════════
        // Get User Order By Id
        // ════════════════════════════════════════════════════
        public async Task<OrderDto?> GetUserOrderByIdAsync(Guid userId, Guid orderId)
        {
            return await _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId == userId && o.Id == orderId)
                .Select(o => ToDto(o))
                .FirstOrDefaultAsync();
        }

        // ════════════════════════════════════════════════════
        // Get Store Orders
        // ════════════════════════════════════════════════════
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
                    ItemsCount = o.Items.Count,
                    StoreId = o.StoreId,
                    CreatedAt = o.CreatedAt
                })
                .ToListAsync();
        }

        // ════════════════════════════════════════════════════
        // Get Store Order By Id
        // ════════════════════════════════════════════════════
        public async Task<OrderDto?> GetStoreOrderByIdAsync(Guid storeId, Guid orderId)
        {
            return await _context.Orders
                .AsNoTracking()
                .Where(o => o.StoreId == storeId && o.Id == orderId)
                .Select(o => ToDto(o))
                .FirstOrDefaultAsync();
        }

        // ════════════════════════════════════════════════════
        // Update Order Status
        // ════════════════════════════════════════════════════
        public async Task<OrderDto?> UpdateOrderStatusAsync(Guid orderId, UpdateOrderStatusDto dto)
        {
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
                orderId, dto.Status);

            return await GetOrderDtoByIdAsync(orderId);
        }

        // ════════════════════════════════════════════════════
        // Helper — Validate Coupon
        // ════════════════════════════════════════════════════
        private async Task<CouponEntity> ValidateCouponAsync(
            string code,
            Guid storeId,
            Guid userId,
            decimal subTotal)
        {
            var coupon = await _context.Coupons
                .FirstOrDefaultAsync(c =>
                    c.Code == code.ToUpper() &&
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
                throw new Exception($"الحد الأدنى لاستخدام الكوبون هو {coupon.MinOrderAmount.Value}");

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

        // ════════════════════════════════════════════════════
        // Helper — Calculate Discount
        // ════════════════════════════════════════════════════
        private static decimal CalculateDiscount(CouponEntity coupon, decimal subTotal)
        {
            decimal discount = 0;

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

        // ════════════════════════════════════════════════════
        // Helper — Generate Order Number
        // ════════════════════════════════════════════════════
        private async Task<string> GenerateOrderNumberAsync()
        {
            var today = DateTime.UtcNow.ToString("yyyyMMdd");

            var todayOrdersCount = await _context.Orders
                .IgnoreQueryFilters()
                .CountAsync(o => o.CreatedAt.Date == DateTime.UtcNow.Date);

            return $"ORD-{today}-{(todayOrdersCount + 1):D4}";
        }

        // ════════════════════════════════════════════════════
        // Helper — Get Order Dto By Id
        // ════════════════════════════════════════════════════
        private async Task<OrderDto?> GetOrderDtoByIdAsync(Guid orderId)
        {
            return await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == orderId)
                .Select(o => ToDto(o))
                .FirstOrDefaultAsync();
        }

        // ════════════════════════════════════════════════════
        // Helper — ToDto
        // ════════════════════════════════════════════════════
        private static OrderDto ToDto(Models.Orders.Order o) => new()
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            Status = o.Status,
            SubTotal = o.SubTotal,
            DiscountAmount = o.DiscountAmount,
            TotalAmount = o.TotalAmount,
            CustomerNotes = o.CustomerNotes,
            StoreNotes = o.StoreNotes,
            DeliveryAddress = o.DeliveryAddress,
            DeliveryCity = o.DeliveryCity,
            DeliveryPhone = o.DeliveryPhone,
            UserId = o.UserId,
            StoreId = o.StoreId,
            CouponId = o.CouponId,
            CouponCode = o.Coupon != null ? o.Coupon.Code : null,
            CreatedAt = o.CreatedAt,
            Items = o.Items.Select(i => new OrderItemDto
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