using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Order;
using onlineStore.Services.Order;
using System.Security.Claims;

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
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            try
            {
                var order = await _orderService.CreateOrderAsync(userId.Value, dto);
                return Ok(order);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpGet("my-orders")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetMyOrders()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var orders = await _orderService.GetUserOrdersAsync(userId.Value);
            return Ok(orders);
        }

      
        [HttpGet("my-orders/{orderId}")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetMyOrderById(Guid orderId)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var order = await _orderService.GetUserOrderByIdAsync(userId.Value, orderId);

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


        private Guid? GetUserId()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(userIdStr, out var userId)
                ? userId
                : null;
        }
    }
}