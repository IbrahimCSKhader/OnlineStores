using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Coupon;
using onlineStore.Services.Coupon;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CouponController : ControllerBase
    {
        private readonly ICouponService _couponService;

        public CouponController(ICouponService couponService)
        {
            _couponService = couponService;
        }

        [HttpGet("store/{storeId}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> GetByStore(Guid storeId)
        {
            var coupons = await _couponService.GetStoreCouponsAsync(storeId);
            return Ok(coupons);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var coupon = await _couponService.GetCouponByIdAsync(id);

            if (coupon == null)
                return NotFound(new { message = "copun does not exist" });

            return Ok(coupon);
        }

        [HttpPost]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> Create([FromBody] CreateCouponDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var coupon = await _couponService.CreateCouponAsync(dto);
                return Ok(coupon);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCouponDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var coupon = await _couponService.UpdateCouponAsync(id, dto);

                if (coupon == null)
                    return NotFound(new { message = "copun does not exist" });

                return Ok(coupon);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _couponService.DeleteCouponAsync(id);

            if (!result)
                return NotFound(new { message = "copun does not exist" });

            return Ok(new { message = "copun deleted done" });
        }
    }
}