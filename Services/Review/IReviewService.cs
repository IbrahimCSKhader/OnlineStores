using onlineStore.DTOs.Review;

namespace onlineStore.Services.Review
{
    public interface IReviewService
    {
        Task<List<ReviewDto>> GetApprovedProductReviewsAsync(Guid productId);
        Task<ReviewDto?> GetUserReviewForProductAsync(Guid storeCustomerId, Guid productId);

        Task<ReviewDto> CreateReviewAsync(Guid storeCustomerId, CreateReviewDto dto);
        Task<ReviewDto?> UpdateReviewAsync(Guid storeCustomerId, Guid reviewId, UpdateReviewDto dto);
        Task<bool> DeleteReviewAsync(Guid storeCustomerId, Guid reviewId);

        Task<List<ReviewDto>> GetStoreReviewsAsync(Guid storeId);
        Task<ReviewDto?> UpdateApprovalAsync(Guid reviewId, UpdateReviewApprovalDto dto);
    }
}
