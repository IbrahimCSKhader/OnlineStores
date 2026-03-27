namespace onlineStore.Security
{
    public interface IStoreOwnershipService
    {
        Task<bool> UserOwnsStoreAsync(Guid storeId, Guid userId, CancellationToken cancellationToken = default);
    }
}