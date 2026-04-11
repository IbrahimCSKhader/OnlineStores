namespace onlineStore.Security
{
    public interface IStoreAuthorizationService
    {
        Task<bool> IsSuperAdminAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<bool> CanManageStoreAsync(Guid userId, Guid storeId, CancellationToken cancellationToken = default);

        Task<bool> CanManageProductAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default);

        Task<bool> CanManageCategoryAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken = default);

        Task<bool> CanManageSectionAsync(Guid userId, Guid sectionId, CancellationToken cancellationToken = default);
    }
}