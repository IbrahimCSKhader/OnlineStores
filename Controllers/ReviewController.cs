using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Review;
using onlineStore.Services.Review;
using System.Security.Claims;

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
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetMyReview(Guid productId)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var review = await _reviewService.GetUserReviewForProductAsync(userId.Value, productId);

            if (review == null)
                return NotFound(new { message = "Review not found" });

            return Ok(review);
        }

        [HttpPost]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Create([FromBody] CreateReviewDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            try
            {
                var review = await _reviewService.CreateReviewAsync(userId.Value, dto);
                return Ok(review);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpPut("{reviewId}")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Update(Guid reviewId, [FromBody] UpdateReviewDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var review = await _reviewService.UpdateReviewAsync(userId.Value, reviewId, dto);

            if (review == null)
                return NotFound(new { message = "Review not found" });

            return Ok(review);
        }


        [HttpDelete("{reviewId}")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Delete(Guid reviewId)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _reviewService.DeleteReviewAsync(userId.Value, reviewId);

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


        private Guid? GetUserId()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(userIdStr, out var userId)
                ? userId
                : null;
        }
    }
}