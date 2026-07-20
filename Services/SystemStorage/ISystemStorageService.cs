using onlineStore.DTOs.SystemStorage;

namespace onlineStore.Services.SystemStorage
{
    public interface ISystemStorageService
    {
        Task<EnsureVariantFoldersResultDto> EnsureVariantFoldersExistAsync(
            CancellationToken cancellationToken = default);

        Task<RemoveEmptyProductImagesFoldersResultDto> RemoveEmptyProductImagesFoldersAsync(
            bool dryRun = true,
            CancellationToken cancellationToken = default);
    }
}
