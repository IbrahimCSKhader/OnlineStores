// Controllers/CartController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Cart;
using onlineStore.Services.Cart;
using System.Security.Claims;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // ← كل الكارت محتاج تسجيل دخول
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
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var cart = await _cartService.GetCartAsync(userId.Value, storeId);
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

            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var cart = await _cartService.AddToCartAsync(userId.Value, dto);
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

            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var cart = await _cartService
                .UpdateCartItemAsync(userId.Value, cartItemId, dto);
            return Ok(cart);
        }


        // ════════════════════════════════════════════════════
        // DELETE api/cart/item/{cartItemId}
        // ════════════════════════════════════════════════════
        [HttpDelete("item/{cartItemId}")]
        public async Task<IActionResult> RemoveItem(Guid cartItemId)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var cart = await _cartService
                .RemoveFromCartAsync(userId.Value, cartItemId);
            return Ok(cart);
        }


        // ════════════════════════════════════════════════════
        // DELETE api/cart/clear/{storeId}
        // ════════════════════════════════════════════════════
        [HttpDelete("clear/{storeId}")]
        public async Task<IActionResult> ClearCart(Guid storeId)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            await _cartService.ClearCartAsync(userId.Value, storeId);
            return Ok(new { message = "تم تفريغ الكارت بنجاح" });
        }


        // ════════════════════════════════════════════════════
        // 🔐 Helper — Get UserId من الـ JWT Token
        // ════════════════════════════════════════════════════
        private Guid? GetUserId()
        {
            var userIdStr = User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(userIdStr, out var userId)
                ? userId
                : null;
        }
    }
}