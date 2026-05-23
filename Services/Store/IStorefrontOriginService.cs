namespace onlineStore.Services.Store
{
    public interface IStorefrontOriginService
    {
        bool IsAllowedOrigin(string? origin);
        bool IsAllowedHost(string? host);
    }
}
