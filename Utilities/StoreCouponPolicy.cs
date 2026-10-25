namespace onlineStore.Utilities;

public static class StoreCouponPolicy
{
    private static readonly Guid AnimalKingdomStoreId = new("A601BE8D-8D2D-4A79-B0E2-272490194669");

    public static bool IsEnabled(Guid storeId) => storeId != AnimalKingdomStoreId;

    public static void EnsureEnabled(Guid storeId)
    {
        if (!IsEnabled(storeId))
            throw new InvalidOperationException("الكوبونات غير متاحة لهذا المتجر.");
    }
}
