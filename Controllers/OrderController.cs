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

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
        }


        [HttpPost]
        [Authorize(Policy = "StoreCustomerOnly")]
        public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null) return Unauthorized();
            if (storeCustomer.Value.StoreId != dto.StoreId) return Forbid();

            try
            {
                var order = await _orderService.CreateOrderAsync(storeCustomer.Value.StoreCustomerId, dto);
                return Ok(order);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpGet("my-orders")]
        [Authorize(Policy = "StoreCustomerOnly")]
        public async Task<IActionResult> GetMyOrders()
        {
            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null) return Unauthorized();

            var orders = await _orderService.GetUserOrdersAsync(storeCustomer.Value.StoreCustomerId);
            return Ok(orders);
        }

      
        [HttpGet("my-orders/{orderId}")]
        [Authorize(Policy = "StoreCustomerOnly")]
        public async Task<IActionResult> GetMyOrderById(Guid orderId)
        {
            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null) return Unauthorized();

            var order = await _orderService.GetUserOrderByIdAsync(storeCustomer.Value.StoreCustomerId, orderId);

            if (order == null)
                return NotFound(new { message = " Order does not exit" });

            return Ok(order);
        }

        [HttpGet("store/{storeId}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> GetStoreOrders(Guid storeId)
        {
            var orders = await _orderService.GetStoreOrdersAsync(storeId);
            return Ok(orders);
        }


        [HttpGet("store/{storeId}/{orderId}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> GetStoreOrderById(Guid storeId, Guid orderId)
        {
            var order = await _orderService.GetStoreOrderByIdAsync(storeId, orderId);

            if (order == null)
                return NotFound(new { message = " Order does not exit" });

            return Ok(order);
        }


        [HttpPut("{orderId}/status")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> UpdateStatus(
            Guid orderId,
            [FromBody] UpdateOrderStatusDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var order = await _orderService.UpdateOrderStatusAsync(orderId, dto);

            if (order == null)
                return NotFound(new { message = " Order does not exit" });

            return Ok(order);
        }


        private (Guid StoreCustomerId, Guid StoreId)? GetStoreCustomerContext()
        {
            var storeCustomerId = User.GetStoreCustomerId();
            var storeId = User.GetStoreCustomerStoreId();

            return storeCustomerId.HasValue && storeId.HasValue
                ? (storeCustomerId.Value, storeId.Value)
                : null;
        }
    }
}
