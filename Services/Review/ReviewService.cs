using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Review;

namespace onlineStore.Services.Review
{
    public class ReviewService : IReviewService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ReviewService> _logger;

        public ReviewService(
            AppDbContext context,
            ILogger<ReviewService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<ReviewDto>> GetApprovedProductReviewsAsync(Guid productId)
        {
            return await _context.Reviews
                .AsNoTracking()
                .Where(r => r.ProductId == productId && r.IsApproved)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => ToDto(r))
                .ToListAsync();
        }


        public async Task<ReviewDto?> GetUserReviewForProductAsync(Guid userId, Guid productId)
        {
            var review = await _context.Reviews
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.UserId == userId && r.ProductId == productId);

            return review == null ? null : ToDto(review);
        }


        public async Task<ReviewDto> CreateReviewAsync(Guid userId, CreateReviewDto dto)
        {
            if (dto.StoreId == Guid.Empty || dto.ProductId == Guid.Empty)
                throw new Exception("Invalid store or product identifier");

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == dto.ProductId);

            if (product == null)
                throw new Exception("Product does not exist");

            if (product.StoreId != dto.StoreId)
                throw new Exception("Product does not belong to this store");

            var alreadyReviewed = await _context.Reviews
                .IgnoreQueryFilters()
                .AnyAsync(r => r.UserId == userId && r.ProductId == dto.ProductId);

            if (alreadyReviewed)
                throw new Exception("You have already reviewed this product");

            var review = new Models.Reviews.Review
            {
                Rating = dto.Rating,
                Comment = dto.Comment?.Trim(),
                IsApproved = false,
                ProductId = dto.ProductId,
                UserId = userId,
                StoreId = dto.StoreId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Review created for product {ProductId} by user {UserId}",
                dto.ProductId, userId);

            return await GetReviewDtoByIdAsync(review.Id)
                   ?? throw new Exception("Failed to load review after creation");
        }


        public async Task<ReviewDto?> UpdateReviewAsync(Guid userId, Guid reviewId, UpdateReviewDto dto)
        {
            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId);

            if (review == null)
                return null;

            if (dto.Rating.HasValue)
                review.Rating = dto.Rating.Value;

            if (dto.Comment != null)
                review.Comment = dto.Comment.Trim();

            // reset approval after edit
            review.IsApproved = false;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Review updated: {ReviewId} by user {UserId}",
                reviewId, userId);

            return await GetReviewDtoByIdAsync(reviewId);
        }


        public async Task<bool> DeleteReviewAsync(Guid userId, Guid reviewId)
        {
            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId);

            if (review == null)
                return false;

            review.IsDeleted = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Review deleted: {ReviewId} by user {UserId}",
                reviewId, userId);

            return true;
        }

        public async Task<List<ReviewDto>> GetStoreReviewsAsync(Guid storeId)
        {
            return await _context.Reviews
                .AsNoTracking()
                .Where(r => r.StoreId == storeId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => ToDto(r))
                .ToListAsync();
        }


        public async Task<ReviewDto?> UpdateApprovalAsync(Guid reviewId, UpdateReviewApprovalDto dto)
        {
            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == reviewId);

            if (review == null)
                return null;

            review.IsApproved = dto.IsApproved;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Review approval updated: {ReviewId} => {IsApproved}",
                reviewId, dto.IsApproved);

            return await GetReviewDtoByIdAsync(reviewId);
        }


        private async Task<ReviewDto?> GetReviewDtoByIdAsync(Guid reviewId)
        {
            return await _context.Reviews
                .AsNoTracking()
                .Where(r => r.Id == reviewId)
                .Select(r => ToDto(r))
                .FirstOrDefaultAsync();
        }


        private static ReviewDto ToDto(Models.Reviews.Review r) => new()
        {
            Id = r.Id,
            Rating = r.Rating,
            Comment = r.Comment,
            IsApproved = r.IsApproved,
            ProductId = r.ProductId,
            UserId = r.UserId,
            StoreId = r.StoreId,
            UserFullName = r.User != null
                ? $"{r.User.FirstName} {r.User.LastName}".Trim()
                : null,
            CreatedAt = r.CreatedAt
        };
    }
}