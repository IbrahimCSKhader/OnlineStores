using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.Models;
using onlineStore.Utilities;

namespace onlineStore.Services.CustomerStore;

public static class CustomerIdentity
{
    public static async Task PreserveOrderSnapshotsAsync(AppDbContext db, StoreCustomer customer, CancellationToken ct = default)
    {
        var orders = await db.Orders.IgnoreQueryFilters().Where(x => x.StoreId == customer.StoreId && x.StoreCustomerId == customer.Id &&
            (x.CustomerName == null || x.CustomerEmail == null)).ToListAsync(ct);
        var name = $"{customer.FirstName} {customer.LastName}".Trim();
        foreach (var order in orders)
        {
            order.CustomerName ??= name[..Math.Min(name.Length, 200)];
            if (!customer.IsGuestProfile) order.CustomerEmail ??= customer.Email;
        }
    }
    // Call inside the order's serializable transaction. Names only disambiguate a phone match.
    public static async Task<StoreCustomer> ResolveAsync(AppDbContext db, Guid storeId, string? name, string? phone,
        Guid? registeredId = null, CancellationToken ct = default)
    {
        StoreCustomer? registered = null;
        if (registeredId.HasValue)
        {
            registered = await db.StoreCustomers.IgnoreQueryFilters().SingleAsync(x => x.StoreId == storeId && x.Id == registeredId, ct);
            name = $"{registered.FirstName} {registered.LastName}";
            phone = registered.Phone ?? phone;
            if (registered.MergedIntoCustomerId.HasValue)
                return await db.StoreCustomers.SingleAsync(x => x.StoreId == storeId && x.Id == registered.MergedIntoCustomerId, ct);
            var previous = await db.Orders.Where(x => x.StoreId == storeId && x.StoreCustomerId == registeredId && x.AccountCustomerId != null)
                .OrderBy(x => x.CreatedAt).Select(x => x.AccountCustomerId).FirstOrDefaultAsync(ct);
            if (previous.HasValue) return await db.StoreCustomers.SingleAsync(x => x.StoreId == storeId && x.Id == previous, ct);
            if (await db.CustomerLedgerEntries.AnyAsync(x => x.StoreId == storeId && x.CustomerId == registeredId, ct)) return registered;
            if (registered.IsDeleted) registered = null;
        }
        var normalized = CustomerPhone.Normalize(phone);
        // Include older rows whose normalized value has not yet been backfilled.
        var candidates = normalized == null ? new List<StoreCustomer>() :
            (await db.StoreCustomers.Where(x => x.StoreId == storeId && x.IsActive && x.MergedIntoCustomerId == null && x.Id != registeredId &&
                (x.NormalizedPhone == normalized || x.NormalizedPhone == null)).ToListAsync(ct))
            .Where(x => CustomerPhone.Normalize(x.Phone) == normalized).ToList();
        if (candidates.Count == 1 && !candidates[0].HasSharedPhone &&
            CustomerPhone.Name($"{candidates[0].FirstName} {candidates[0].LastName}") == CustomerPhone.Name(name) && !string.IsNullOrWhiteSpace(name))
        {
            candidates[0].NormalizedPhone = normalized;
            if (registered != null && registered.Id != candidates[0].Id)
            {
                registered.MergedIntoCustomerId = candidates[0].Id;
                db.CustomerAccountAudits.Add(new() { StoreId = storeId, Action = "RegisteredProfileLinked", SourceId = registered.Id,
                    TargetId = candidates[0].Id, Reason = "Exact normalized phone and full name; storefront authorization unchanged" });
            }
            return candidates[0];
        }
        if (registered != null) return registered;
        foreach (var candidate in candidates) candidate.HasSharedPhone = true;
        var parts = (name ?? "عميل").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var first = parts.FirstOrDefault() ?? "عميل";
        var last = parts.Length > 1 ? parts[1] : "";
        var customer = new StoreCustomer { StoreId = storeId, FirstName = first[..Math.Min(first.Length, 100)], LastName = last[..Math.Min(last.Length, 100)],
            Phone = phone, NormalizedPhone = normalized, IsGuestProfile = true,
            Email = $"guest-{Guid.NewGuid():N}@profiles.invalid", PasswordHash = "", HasSharedPhone = candidates.Count > 0 };
        db.StoreCustomers.Add(customer);
        return customer;
    }
}
