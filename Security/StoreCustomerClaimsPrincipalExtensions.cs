using System.Security.Claims;

namespace onlineStore.Security
{
    public static class StoreCustomerClaimsPrincipalExtensions
    {
        public static bool IsStoreCustomer(this ClaimsPrincipal? user)
        {
            return string.Equals(
                user?.FindFirstValue(StoreCustomerClaimTypes.AccountType),
                StoreCustomerClaimTypes.StoreCustomerAccountType,
                StringComparison.Ordinal);
        }

        public static Guid? GetStoreCustomerId(this ClaimsPrincipal? user)
        {
            if (!user.IsStoreCustomer())
                return null;

            var value = user?.FindFirstValue(StoreCustomerClaimTypes.StoreCustomerId)
                ?? user?.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var id) ? id : null;
        }

        public static Guid? GetStoreCustomerStoreId(this ClaimsPrincipal? user)
        {
            if (!user.IsStoreCustomer())
                return null;

            var value = user?.FindFirstValue(StoreCustomerClaimTypes.StoreId);

            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}
