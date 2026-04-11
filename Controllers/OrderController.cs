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
                "OrderController.Create started. StoreId: {StoreId}, HasCoupon: {HasCoupon}",
                dto?.StoreId,
                !string.IsNullOrWhiteSpace(dto?.CouponCode));

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("OrderController.Create rejected بسبب ModelState غير صالح.");
                return BadRequest(ModelState);
            }

            if (dto == null)
            {
                _logger.LogWarning("OrderController.Create rejected because request body is null.");
                return BadRequest(new { message = "بيانات الطلب غير صالحة" });
            }

            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null)
            {
                _logger.LogWarning("OrderController.Create failed: store customer context not found.");
                return Unauthorized();
            }

            if (storeCustomer.Value.StoreId != dto.StoreId)
            {
                _logger.LogWarning(
                    "OrderController.Create forbidden. TokenStoreId: {TokenStoreId}, BodyStoreId: {BodyStoreId}, StoreCustomerId: {StoreCustomerId}",
                    storeCustomer.Value.StoreId,
                    dto.StoreId,
                    storeCustomer.Value.StoreCustomerId);
                return Forbid();
            }

            try
            {
                var order = await _orderService.CreateOrderAsync(storeCustomer.Value.StoreCustomerId, dto);
                _logger.LogInformation(
                    "OrderController.Create succeeded. OrderId: {OrderId}, StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                    order.Id,
                    storeCustomer.Value.StoreCustomerId,
                    dto.StoreId);
                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "OrderController.Create failed. StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                    storeCustomer.Value.StoreCustomerId,
                    dto.StoreId);
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpGet("my-orders")]
        [Authorize(Policy = "StoreCustomerOnly")]
        public async Task<IActionResult> GetMyOrders()
        {
            _logger.LogInformation("OrderController.GetMyOrders started.");
            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null)
            {
                _logger.LogWarning("OrderController.GetMyOrders failed: store customer context not found.");
                return Unauthorized();
            }

            var orders = await _orderService.GetUserOrdersAsync(storeCustomer.Value.StoreCustomerId);
            _logger.LogInformation(
                "OrderController.GetMyOrders succeeded. StoreCustomerId: {StoreCustomerId}, Count: {Count}",
                storeCustomer.Value.StoreCustomerId,
                orders.Count);
            return Ok(orders);
        }

      
        [HttpGet("my-orders/{orderId}")]
        [Authorize(Policy = "StoreCustomerOnly")]
        public async Task<IActionResult> GetMyOrderById(Guid orderId)
        {
            _logger.LogInformation("OrderController.GetMyOrderById started. OrderId: {OrderId}", orderId);
            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null)
            {
                _logger.LogWarning(
                    "OrderController.GetMyOrderById failed: store customer context not found. OrderId: {OrderId}",
                    orderId);
                return Unauthorized();
            }

            var order = await _orderService.GetUserOrderByIdAsync(storeCustomer.Value.StoreCustomerId, orderId);

            if (order == null)
            {
                _logger.LogWarning(
                    "OrderController.GetMyOrderById not found. StoreCustomerId: {StoreCustomerId}, OrderId: {OrderId}",
                    storeCustomer.Value.StoreCustomerId,
                    orderId);
                return NotFound(new { message = " Order does not exit" });
            }

            _logger.LogInformation(
                "OrderController.GetMyOrderById succeeded. StoreCustomerId: {StoreCustomerId}, OrderId: {OrderId}",
                storeCustomer.Value.StoreCustomerId,
                orderId);
            return Ok(order);
        }

        [HttpGet("store/{storeId}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> GetStoreOrders(Guid storeId)
        {
            _logger.LogInformation("OrderController.GetStoreOrders started. StoreId: {StoreId}", storeId);
            var orders = await _orderService.GetStoreOrdersAsync(storeId);
            _logger.LogInformation(
                "OrderController.GetStoreOrders succeeded. StoreId: {StoreId}, Count: {Count}",
                storeId,
                orders.Count);
            return Ok(orders);
        }


        [HttpGet("store/{storeId}/{orderId}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> GetStoreOrderById(Guid storeId, Guid orderId)
        {
            _logger.LogInformation(
                "OrderController.GetStoreOrderById started. StoreId: {StoreId}, OrderId: {OrderId}",
                storeId,
                orderId);
            var order = await _orderService.GetStoreOrderByIdAsync(storeId, orderId);

            if (order == null)
            {
                _logger.LogWarning(
                    "OrderController.GetStoreOrderById not found. StoreId: {StoreId}, OrderId: {OrderId}",
                    storeId,
                    orderId);
                return NotFound(new { message = " Order does not exit" });
            }

            _logger.LogInformation(
                "OrderController.GetStoreOrderById succeeded. StoreId: {StoreId}, OrderId: {OrderId}",
                storeId,
                orderId);
            return Ok(order);
        }


        [HttpPut("{orderId}/status")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> UpdateStatus(
            Guid orderId,
            [FromBody] UpdateOrderStatusDto dto)
        {
            _logger.LogInformation(
                "OrderController.UpdateStatus started. OrderId: {OrderId}, NewStatus: {Status}",
                orderId,
                dto?.Status);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "OrderController.UpdateStatus rejected بسبب ModelState غير صالح. OrderId: {OrderId}",
                    orderId);
                return BadRequest(ModelState);
            }

            if (dto == null)
            {
                _logger.LogWarning(
                    "OrderController.UpdateStatus rejected because request body is null. OrderId: {OrderId}",
                    orderId);
                return BadRequest(new { message = "بيانات الطلب غير صالحة" });
            }

            var order = await _orderService.UpdateOrderStatusAsync(orderId, dto);

            if (order == null)
            {
                _logger.LogWarning(
                    "OrderController.UpdateStatus not found. OrderId: {OrderId}",
                    orderId);
                return NotFound(new { message = " Order does not exit" });
            }

            _logger.LogInformation(
                "OrderController.UpdateStatus succeeded. OrderId: {OrderId}, Status: {Status}",
                orderId,
                dto.Status);
            return Ok(order);
        }


        private (Guid StoreCustomerId, Guid StoreId)? GetStoreCustomerContext()
        {
            var storeCustomerId = User.GetStoreCustomerId();
            var storeId = User.GetStoreCustomerStoreId();

            _logger.LogDebug(
                "OrderController.GetStoreCustomerContext evaluated. HasStoreCustomerId: {HasStoreCustomerId}, HasStoreId: {HasStoreId}",
                storeCustomerId.HasValue,
                storeId.HasValue);

            return storeCustomerId.HasValue && storeId.HasValue
                ? (storeCustomerId.Value, storeId.Value)
                : null;
        }
    }
}
