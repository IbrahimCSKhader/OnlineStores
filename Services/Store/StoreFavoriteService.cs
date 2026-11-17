using System.Data;
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.Models;
using onlineStore.Models.Enums;

namespace onlineStore.Services.Store;

public class StoreFavoriteService(AppDbContext db)
{
    public Task<bool> IsRegisteredCustomerAsync(Guid storeId, Guid customerId, CancellationToken ct) =>
        db.StoreCustomers.AnyAsync(x => x.Id == customerId && x.StoreId == storeId
            && x.IsActive && !x.IsGuestProfile && x.MergedIntoCustomerId == null, ct);

    public Task<Guid[]> GetAsync(Guid storeId, Guid customerId, CancellationToken ct) =>
        CustomerFavorites(storeId, customerId)
            .Where(x => PublicProducts(storeId).Any(product => product.Id == x.ProductId))
            .Select(x => x.ProductId)
            .ToArrayAsync(ct);

    public Task<bool> AddAsync(Guid storeId, Guid customerId, Guid productId, CancellationToken ct) =>
        AddMissingAsync(storeId, customerId, [productId], requireAll: true, ct);

    public async Task<Guid[]> MergeAsync(Guid storeId, Guid customerId, Guid[] productIds, CancellationToken ct)
    {
        await AddMissingAsync(storeId, customerId, productIds.Distinct().ToArray(), requireAll: false, ct);
        return await GetAsync(storeId, customerId, ct);
    }

    public Task<int> RemoveAsync(Guid storeId, Guid customerId, Guid productId, CancellationToken ct) =>
        CustomerFavorites(storeId, customerId).Where(x => x.ProductId == productId).ExecuteDeleteAsync(ct);

    private IQueryable<StoreFavorite> CustomerFavorites(Guid storeId, Guid customerId) =>
        db.StoreFavorites.Where(x => x.StoreId == storeId && x.CustomerId == customerId);

    private IQueryable<Models.Product> PublicProducts(Guid storeId) =>
        db.Products.Where(x => x.StoreId == storeId && x.Status == ProductStatus.Active && x.Store.IsActive);

    private Task<bool> AddMissingAsync(Guid storeId, Guid customerId, Guid[] ids, bool requireAll, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var validIds = await PublicProducts(storeId)
                .Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToArrayAsync(ct);
            if (requireAll && validIds.Length != ids.Length)
                return false;

            var existingIds = await CustomerFavorites(storeId, customerId)
                .Where(x => validIds.Contains(x.ProductId)).Select(x => x.ProductId).ToArrayAsync(ct);
            var additions = validIds.Except(existingIds)
                .Select(id => new StoreFavorite { StoreId = storeId, CustomerId = customerId, ProductId = id })
                .ToArray();

            db.StoreFavorites.AddRange(additions);
            try
            {
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return true;
            }
            finally
            {
                foreach (var addition in additions)
                    db.Entry(addition).State = EntityState.Detached;
            }
        }, ct);
}
