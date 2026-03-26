using onlineStore.DTOs.Review;

namespace onlineStore.Services.Review
{
    public interface IReviewService
    {
        Task<List<ReviewDto>> GetApprovedProductReviewsAsync(Guid productId);
        Task<ReviewDto?> GetUserReviewForProductAsync(Guid userId, Guid productId);

        Task<ReviewDto> CreateReviewAsync(Guid userId, CreateReviewDto dto);
        Task<ReviewDto?> UpdateReviewAsync(Guid userId, Guid reviewId, UpdateReviewDto dto);
        Task<bool> DeleteReviewAsync(Guid userId, Guid reviewId);

        Task<List<ReviewDto>> GetStoreReviewsAsync(Guid storeId);
        Task<ReviewDto?> UpdateApprovalAsync(Guid reviewId, UpdateReviewApprovalDto dto);
    }
}
