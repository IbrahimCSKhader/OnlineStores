using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Order;
using onlineStore.Security;
using onlineStore.Services.Order;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrderController> _logger;

        public OrderController(IOrderService orderService, ILogger<OrderController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }


        [HttpPost]
        [Authorize(Policy = "StoreCustomerOnly")]
        public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
        {
            _logger.LogInformation(
                "order=> controller:create:start Request={@Request}",
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
                        dto.DeliveryPhone,
                        HasCoupon = !string.IsNullOrWhiteSpace(dto.CouponCode)
                    });

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("order=> controller:create:invalid-model");
                return BadRequest(ModelState);
            }

            if (dto == null)
            {
                _logger.LogWarning("order=> controller:create:null-body");
                return BadRequest(new { message = "بيانات الطلب غير صالحة" });
            }

            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null)
            {
                _logger.LogWarning("order=> controller:create:missing-store-customer-context");
                return Unauthorized();
            }

            if (storeCustomer.Value.StoreId != dto.StoreId)
            {
                _logger.LogWarning(
                    "order=> controller:create:forbidden TokenStoreId={TokenStoreId} BodyStoreId={BodyStoreId} StoreCustomerId={StoreCustomerId}",
                    storeCustomer.Value.StoreId,
                    dto.StoreId,
                    storeCustomer.Value.StoreCustomerId);
                return Forbid();
            }

            try
            {
                var order = await _orderService.CreateOrderAsync(storeCustomer.Value.StoreCustomerId, dto);
                _logger.LogInformation(
                    "order=> controller:create:result OrderId={OrderId} StoreCustomerId={StoreCustomerId} StoreId={StoreId} Order={@Order}",
                    order.Id,
                    storeCustomer.Value.StoreCustomerId,
                    dto.StoreId,
                    new
                    {
                        order.Id,
                        order.OrderNumber,
                        order.Title,
                        order.Status,
                        order.TotalAmount,
                        ItemsCount = order.Items.Count
                    });
                return Ok(order);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    ex,
                    "order=> controller:create:unauthorized StoreCustomerId={StoreCustomerId} StoreId={StoreId}",
                    storeCustomer.Value.StoreCustomerId,
                    dto.StoreId);
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "order=> controller:create:error StoreCustomerId={StoreCustomerId} StoreId={StoreId}",
                    storeCustomer.Value.StoreCustomerId,
                    dto.StoreId);
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpGet("my-orders")]
        [Authorize(Policy = "StoreCustomerOnly")]
        public async Task<IActionResult> GetMyOrders()
        {
            _logger.LogInformation("order=> controller:get-my-orders:start");
            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null)
            {
                _logger.LogWarning("order=> controller:get-my-orders:missing-store-customer-context");
                return Unauthorized();
            }

            var orders = await _orderService.GetUserOrdersAsync(storeCustomer.Value.StoreCustomerId);
            _logger.LogInformation(
                "order=> controller:get-my-orders:result StoreCustomerId={StoreCustomerId} Count={Count}",
                storeCustomer.Value.StoreCustomerId,
                orders.Count);
            return Ok(orders);
        }

      
        [HttpGet("my-orders/{orderId}")]
        [Authorize(Policy = "StoreCustomerOnly")]
        public async Task<IActionResult> GetMyOrderById(Guid orderId)
        {
            _logger.LogInformation("order=> controller:get-my-order-by-id:start OrderId={OrderId}", orderId);
            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null)
            {
                _logger.LogWarning(
                    "order=> controller:get-my-order-by-id:missing-store-customer-context OrderId={OrderId}",
                    orderId);
                return Unauthorized();
            }

            var order = await _orderService.GetUserOrderByIdAsync(storeCustomer.Value.StoreCustomerId, orderId);

            if (order == null)
            {
                _logger.LogWarning(
                    "order=> controller:get-my-order-by-id:not-found StoreCustomerId={StoreCustomerId} OrderId={OrderId}",
                    storeCustomer.Value.StoreCustomerId,
                    orderId);
                return NotFound(new { message = " Order does not exit" });
            }

            _logger.LogInformation(
                "order=> controller:get-my-order-by-id:result StoreCustomerId={StoreCustomerId} OrderId={OrderId}",
                storeCustomer.Value.StoreCustomerId,
                orderId);
            return Ok(order);
        }

        [HttpGet("store/{storeId}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> GetStoreOrders(Guid storeId)
        {
            _logger.LogInformation("order=> controller:get-store-orders:start StoreId={StoreId}", storeId);

            try
            {
                var orders = await _orderService.GetStoreOrdersAsync(storeId);
                _logger.LogInformation(
                    "order=> controller:get-store-orders:result StoreId={StoreId} Count={Count}",
                    storeId,
                    orders.Count);
                return Ok(orders);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    ex,
                    "order=> controller:get-store-orders:forbidden StoreId={StoreId}",
                    storeId);
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
        }


        [HttpGet("store/{storeId}/{orderId}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> GetStoreOrderById(Guid storeId, Guid orderId)
        {
            _logger.LogInformation(
                "order=> controller:get-store-order-by-id:start StoreId={StoreId} OrderId={OrderId}",
                storeId,
                orderId);

            try
            {
                var order = await _orderService.GetStoreOrderByIdAsync(storeId, orderId);

                if (order == null)
                {
                    _logger.LogWarning(
                        "order=> controller:get-store-order-by-id:not-found StoreId={StoreId} OrderId={OrderId}",
                        storeId,
                        orderId);
                    return NotFound(new { message = " Order does not exit" });
                }

                _logger.LogInformation(
                    "order=> controller:get-store-order-by-id:result StoreId={StoreId} OrderId={OrderId}",
                    storeId,
                    orderId);
                return Ok(order);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    ex,
                    "order=> controller:get-store-order-by-id:forbidden StoreId={StoreId} OrderId={OrderId}",
                    storeId,
                    orderId);
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
        }


        [HttpPut("{orderId}/status")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> UpdateStatus(
            Guid orderId,
            [FromBody] UpdateOrderStatusDto dto)
        {
            _logger.LogInformation(
                "order=> controller:update-status:start OrderId={OrderId} NewStatus={Status}",
                orderId,
                dto?.Status);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "order=> controller:update-status:invalid-model OrderId={OrderId}",
                    orderId);
                return BadRequest(ModelState);
            }

            if (dto == null)
            {
                _logger.LogWarning(
                    "order=> controller:update-status:null-body OrderId={OrderId}",
                    orderId);
                return BadRequest(new { message = "بيانات الطلب غير صالحة" });
            }

            try
            {
                var order = await _orderService.UpdateOrderStatusAsync(orderId, dto);

                if (order == null)
                {
                    _logger.LogWarning(
                        "order=> controller:update-status:not-found OrderId={OrderId}",
                        orderId);
                    return NotFound(new { message = " Order does not exit" });
                }

                _logger.LogInformation(
                    "order=> controller:update-status:result OrderId={OrderId} Status={Status}",
                    orderId,
                    dto.Status);
                return Ok(order);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    ex,
                    "order=> controller:update-status:forbidden OrderId={OrderId}",
                    orderId);
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "order=> controller:update-status:error OrderId={OrderId}",
                    orderId);
                return BadRequest(new { message = ex.Message });
            }
        }


        private (Guid StoreCustomerId, Guid StoreId)? GetStoreCustomerContext()
        {
            var storeCustomerId = User.GetStoreCustomerId();
            var storeId = User.GetStoreCustomerStoreId();

            _logger.LogDebug(
                "order=> controller:store-customer-context HasStoreCustomerId={HasStoreCustomerId} HasStoreId={HasStoreId}",
                storeCustomerId.HasValue,
                storeId.HasValue);

            return storeCustomerId.HasValue && storeId.HasValue
                ? (storeCustomerId.Value, storeId.Value)
                : null;
        }
    }
}
