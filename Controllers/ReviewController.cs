using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Review;
using onlineStore.Security;
using onlineStore.Services.Review;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReviewController : ControllerBase
    {
        private readonly IReviewService _reviewService;

        public ReviewController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        [HttpGet("product/{productId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetApprovedByProduct(Guid productId)
        {
            var reviews = await _reviewService.GetApprovedProductReviewsAsync(productId);
            return Ok(reviews);
        }


        [HttpGet("product/{productId}/my-review")]
        [Authorize(Policy = "StoreCustomerOnly")]
        public async Task<IActionResult> GetMyReview(Guid productId)
        {
            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null) return Unauthorized();

            var review = await _reviewService.GetUserReviewForProductAsync(storeCustomer.Value.StoreCustomerId, productId);

            if (review == null)
                return NotFound(new { message = "Review not found" });

            return Ok(review);
        }

        [HttpPost]
        [Authorize(Policy = "StoreCustomerOnly")]
        public async Task<IActionResult> Create([FromBody] CreateReviewDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null) return Unauthorized();
            if (storeCustomer.Value.StoreId != dto.StoreId) return Forbid();

            try
            {
                var review = await _reviewService.CreateReviewAsync(storeCustomer.Value.StoreCustomerId, dto);
                return Ok(review);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpPut("{reviewId}")]
        [Authorize(Policy = "StoreCustomerOnly")]
        public async Task<IActionResult> Update(Guid reviewId, [FromBody] UpdateReviewDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null) return Unauthorized();

            var review = await _reviewService.UpdateReviewAsync(storeCustomer.Value.StoreCustomerId, reviewId, dto);

            if (review == null)
                return NotFound(new { message = "Review not found" });

            return Ok(review);
        }


        [HttpDelete("{reviewId}")]
        [Authorize(Policy = "StoreCustomerOnly")]
        public async Task<IActionResult> Delete(Guid reviewId)
        {
            var storeCustomer = GetStoreCustomerContext();
            if (storeCustomer == null) return Unauthorized();

            var result = await _reviewService.DeleteReviewAsync(storeCustomer.Value.StoreCustomerId, reviewId);

            if (!result)
                return NotFound(new { message = "Review not found" });

            return Ok(new { message = "Review deleted successfully" });
        }


        [HttpGet("store/{storeId}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> GetStoreReviews(Guid storeId)
        {
            var reviews = await _reviewService.GetStoreReviewsAsync(storeId);
            return Ok(reviews);
        }

        [HttpPut("{reviewId}/approval")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> UpdateApproval(
            Guid reviewId,
            [FromBody] UpdateReviewApprovalDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var review = await _reviewService.UpdateApprovalAsync(reviewId, dto);

            if (review == null)
                return NotFound(new { message = "Review not found" });

            return Ok(review);
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
