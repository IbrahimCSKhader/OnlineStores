// Controllers/CartController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Cart;
using onlineStore.Security;
using onlineStore.Services.Cart;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "StoreCustomerOnly")]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;

        public CartController(ICartService cartService)
        {
            _cartService = cartService;
        }


        // ════════════════════════════════════════════════════
        // GET api/cart/{storeId}
        // ════════════════════════════════════════════════════
        [HttpGet("{storeId}")]
        public async Task<IActionResult> GetCart(Guid storeId)
        {
            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null) return Unauthorized();
            if (storeCustomer.Value.StoreId != storeId) return Forbid();

            var cart = await _cartService.GetCartAsync(storeCustomer.Value.StoreCustomerId, storeId);
            return Ok(cart);
        }


        // ════════════════════════════════════════════════════
        // POST api/cart/add
        // ════════════════════════════════════════════════════
        [HttpPost("add")]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null) return Unauthorized();
            if (storeCustomer.Value.StoreId != dto.StoreId) return Forbid();

            var cart = await _cartService.AddToCartAsync(storeCustomer.Value.StoreCustomerId, dto);
            return Ok(cart);
        }


        // ════════════════════════════════════════════════════
        // PUT api/cart/item/{cartItemId}
        // ════════════════════════════════════════════════════
        [HttpPut("item/{cartItemId}")]
        public async Task<IActionResult> UpdateItem(
            Guid cartItemId, [FromBody] UpdateCartItemDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null) return Unauthorized();

            var cart = await _cartService
                .UpdateCartItemAsync(storeCustomer.Value.StoreCustomerId, cartItemId, dto);
            return Ok(cart);
        }


        // ════════════════════════════════════════════════════
        // DELETE api/cart/item/{cartItemId}
        // ════════════════════════════════════════════════════
        [HttpDelete("item/{cartItemId}")]
        public async Task<IActionResult> RemoveItem(Guid cartItemId)
        {
            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null) return Unauthorized();

            var cart = await _cartService
                .RemoveFromCartAsync(storeCustomer.Value.StoreCustomerId, cartItemId);
            return Ok(cart);
        }


        // ════════════════════════════════════════════════════
        // DELETE api/cart/clear/{storeId}
        // ════════════════════════════════════════════════════
        [HttpDelete("clear/{storeId}")]
        public async Task<IActionResult> ClearCart(Guid storeId)
        {
            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null) return Unauthorized();
            if (storeCustomer.Value.StoreId != storeId) return Forbid();

            await _cartService.ClearCartAsync(storeCustomer.Value.StoreCustomerId, storeId);
            return Ok(new { message = "تم تفريغ الكارت بنجاح" });
        }


        // ════════════════════════════════════════════════════
        // 🔐 Helper — Get UserId من الـ JWT Token
        // ════════════════════════════════════════════════════
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
