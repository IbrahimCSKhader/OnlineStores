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
        private readonly ILogger<CartController> _logger;

        public CartController(ICartService cartService, ILogger<CartController> logger)
        {
            _cartService = cartService;
            _logger = logger;
        }

        [HttpGet("{storeId}")]
        public async Task<IActionResult> GetCart(Guid storeId)
        {
            _logger.LogInformation("CartController.GetCart started. StoreId: {StoreId}", storeId);

            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null)
            {
                _logger.LogWarning(
                    "CartController.GetCart failed: store customer context not found. StoreId: {StoreId}",
                    storeId);
                return Unauthorized();
            }

            if (storeCustomer.Value.StoreId != storeId)
            {
                _logger.LogWarning(
                    "CartController.GetCart forbidden. TokenStoreId: {TokenStoreId}, RouteStoreId: {RouteStoreId}, StoreCustomerId: {StoreCustomerId}",
                    storeCustomer.Value.StoreId,
                    storeId,
                    storeCustomer.Value.StoreCustomerId);
                return Forbid();
            }

            try
            {
                var cart = await _cartService.GetCartAsync(storeCustomer.Value.StoreCustomerId, storeId);

                _logger.LogInformation(
                    "CartController.GetCart succeeded. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}, CartId: {CartId}, ItemsCount: {ItemsCount}",
                    storeId,
                    storeCustomer.Value.StoreCustomerId,
                    cart.Id,
                    cart.Items.Count);
                return Ok(cart);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    ex,
                    "CartController.GetCart forbidden by service. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                    storeId,
                    storeCustomer.Value.StoreCustomerId);
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "CartController.GetCart failed. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                    storeId,
                    storeCustomer.Value.StoreCustomerId);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartDto dto)
        {
            _logger.LogInformation(
                "CartController.AddToCart started. StoreId: {StoreId}, ProductId: {ProductId}, VariantId: {VariantId}, Quantity: {Quantity}",
                dto?.StoreId,
                dto?.ProductId,
                dto?.VariantId,
                dto?.Quantity);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("CartController.AddToCart rejected بسبب ModelState غير صالح.");
                return BadRequest(ModelState);
            }

            if (dto == null)
            {
                _logger.LogWarning("CartController.AddToCart rejected because request body is null.");
                return BadRequest(new { message = "بيانات الطلب غير صالحة" });
            }

            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null)
            {
                _logger.LogWarning(
                    "CartController.AddToCart failed: store customer context not found. StoreId: {StoreId}",
                    dto?.StoreId);
                return Unauthorized();
            }

            if (storeCustomer.Value.StoreId != dto.StoreId)
            {
                _logger.LogWarning(
                    "CartController.AddToCart forbidden. TokenStoreId: {TokenStoreId}, BodyStoreId: {BodyStoreId}, StoreCustomerId: {StoreCustomerId}",
                    storeCustomer.Value.StoreId,
                    dto.StoreId,
                    storeCustomer.Value.StoreCustomerId);
                return Forbid();
            }

            try
            {
                var cart = await _cartService.AddToCartAsync(storeCustomer.Value.StoreCustomerId, dto);

                _logger.LogInformation(
                    "CartController.AddToCart succeeded. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}, CartId: {CartId}, ItemsCount: {ItemsCount}",
                    dto.StoreId,
                    storeCustomer.Value.StoreCustomerId,
                    cart.Id,
                    cart.Items.Count);
                return Ok(cart);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    ex,
                    "CartController.AddToCart forbidden by service. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}, ProductId: {ProductId}, VariantId: {VariantId}",
                    dto.StoreId,
                    storeCustomer.Value.StoreCustomerId,
                    dto.ProductId,
                    dto.VariantId);
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "CartController.AddToCart failed. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}, ProductId: {ProductId}, VariantId: {VariantId}, Quantity: {Quantity}",
                    dto.StoreId,
                    storeCustomer.Value.StoreCustomerId,
                    dto.ProductId,
                    dto.VariantId,
                    dto.Quantity);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("item/{cartItemId}")]
        public async Task<IActionResult> UpdateItem(Guid cartItemId, [FromBody] UpdateCartItemDto dto)
        {
            _logger.LogInformation(
                "CartController.UpdateItem started. CartItemId: {CartItemId}, Quantity: {Quantity}",
                cartItemId,
                dto?.Quantity);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "CartController.UpdateItem rejected بسبب ModelState غير صالح. CartItemId: {CartItemId}",
                    cartItemId);
                return BadRequest(ModelState);
            }

            if (dto == null)
            {
                _logger.LogWarning(
                    "CartController.UpdateItem rejected because request body is null. CartItemId: {CartItemId}",
                    cartItemId);
                return BadRequest(new { message = "بيانات الطلب غير صالحة" });
            }

            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null)
            {
                _logger.LogWarning(
                    "CartController.UpdateItem failed: store customer context not found. CartItemId: {CartItemId}",
                    cartItemId);
                return Unauthorized();
            }

            try
            {
                var cart = await _cartService.UpdateCartItemAsync(storeCustomer.Value.StoreCustomerId, cartItemId, dto);

                _logger.LogInformation(
                    "CartController.UpdateItem succeeded. CartItemId: {CartItemId}, StoreCustomerId: {StoreCustomerId}, CartId: {CartId}, ItemsCount: {ItemsCount}",
                    cartItemId,
                    storeCustomer.Value.StoreCustomerId,
                    cart.Id,
                    cart.Items.Count);
                return Ok(cart);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    ex,
                    "CartController.UpdateItem forbidden by service. CartItemId: {CartItemId}, StoreCustomerId: {StoreCustomerId}",
                    cartItemId,
                    storeCustomer.Value.StoreCustomerId);
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "CartController.UpdateItem failed. CartItemId: {CartItemId}, StoreCustomerId: {StoreCustomerId}, Quantity: {Quantity}",
                    cartItemId,
                    storeCustomer.Value.StoreCustomerId,
                    dto.Quantity);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("item/{cartItemId}")]
        public async Task<IActionResult> RemoveItem(Guid cartItemId)
        {
            _logger.LogInformation(
                "CartController.RemoveItem started. CartItemId: {CartItemId}",
                cartItemId);

            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null)
            {
                _logger.LogWarning(
                    "CartController.RemoveItem failed: store customer context not found. CartItemId: {CartItemId}",
                    cartItemId);
                return Unauthorized();
            }

            try
            {
                var cart = await _cartService.RemoveFromCartAsync(storeCustomer.Value.StoreCustomerId, cartItemId);

                _logger.LogInformation(
                    "CartController.RemoveItem succeeded. CartItemId: {CartItemId}, StoreCustomerId: {StoreCustomerId}, CartId: {CartId}, ItemsCount: {ItemsCount}",
                    cartItemId,
                    storeCustomer.Value.StoreCustomerId,
                    cart.Id,
                    cart.Items.Count);
                return Ok(cart);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    ex,
                    "CartController.RemoveItem forbidden by service. CartItemId: {CartItemId}, StoreCustomerId: {StoreCustomerId}",
                    cartItemId,
                    storeCustomer.Value.StoreCustomerId);
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "CartController.RemoveItem failed. CartItemId: {CartItemId}, StoreCustomerId: {StoreCustomerId}",
                    cartItemId,
                    storeCustomer.Value.StoreCustomerId);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("clear/{storeId}")]
        public async Task<IActionResult> ClearCart(Guid storeId)
        {
            _logger.LogInformation("CartController.ClearCart started. StoreId: {StoreId}", storeId);

            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null)
            {
                _logger.LogWarning(
                    "CartController.ClearCart failed: store customer context not found. StoreId: {StoreId}",
                    storeId);
                return Unauthorized();
            }

            if (storeCustomer.Value.StoreId != storeId)
            {
                _logger.LogWarning(
                    "CartController.ClearCart forbidden. TokenStoreId: {TokenStoreId}, RouteStoreId: {RouteStoreId}, StoreCustomerId: {StoreCustomerId}",
                    storeCustomer.Value.StoreId,
                    storeId,
                    storeCustomer.Value.StoreCustomerId);
                return Forbid();
            }

            try
            {
                await _cartService.ClearCartAsync(storeCustomer.Value.StoreCustomerId, storeId);
                _logger.LogInformation(
                    "CartController.ClearCart succeeded. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                    storeId,
                    storeCustomer.Value.StoreCustomerId);
                return Ok(new { message = "تم تفريغ الكارت بنجاح" });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    ex,
                    "CartController.ClearCart forbidden by service. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                    storeId,
                    storeCustomer.Value.StoreCustomerId);
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "CartController.ClearCart failed. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                    storeId,
                    storeCustomer.Value.StoreCustomerId);
                return BadRequest(new { message = ex.Message });
            }
        }

        private (Guid StoreCustomerId, Guid StoreId)? GetStoreCustomerContext()
        {
            var storeCustomerId = User.GetStoreCustomerId();
            var storeId = User.GetStoreCustomerStoreId();

            _logger.LogDebug(
                "CartController.GetStoreCustomerContext evaluated. HasStoreCustomerId: {HasStoreCustomerId}, HasStoreId: {HasStoreId}",
                storeCustomerId.HasValue,
                storeId.HasValue);

            return storeCustomerId.HasValue && storeId.HasValue
                ? (storeCustomerId.Value, storeId.Value)
                : null;
        }
    }
}
