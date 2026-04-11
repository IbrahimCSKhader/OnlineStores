using onlineStore.Models.Identity;

namespace onlineStore.Security
{
    public interface IStoreAccountBoundaryService
    {
        Task<bool> IsStoreOwnerEmailAsync(
            Guid storeId,
            string email,
            CancellationToken cancellationToken = default);

        Task<AppUser?> GetActiveStoreOwnerAsync(
            Guid ownerId,
            CancellationToken cancellationToken = default);
    }
}
