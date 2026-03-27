namespace onlineStore.Security
{
    public interface ICurrentUserService
    {
        Guid? UserId { get; }
        bool IsAuthenticated { get; }
        bool IsSuperAdmin { get; }
        bool IsStoreOwner { get; }
    }
}