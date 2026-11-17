using System.Data;
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.Models.Enums;
using onlineStore.Models.Reviews;

namespace onlineStore.Services.Store;

public class StoreStatisticsService(AppDbContext db)
{
    public Task<StoreStatistics?> GetAsync(Guid storeId, CancellationToken ct) =>
        db.Stores.Where(x => x.Id == storeId && x.IsActive)
            .Select(x => new StoreStatistics(
                x.VisitCount,
                db.Products.Count(product => product.StoreId == x.Id && product.Status == ProductStatus.Active),
                db.StoreRatings.Where(rating => rating.StoreId == x.Id).Average(rating => (double?)rating.Score),
                db.StoreRatings.Count(rating => rating.StoreId == x.Id)))
            .SingleOrDefaultAsync(ct);

    public async Task<StoreStatistics?> RateAsync(Guid storeId, Guid voterId, bool isGuest, int score, CancellationToken ct)
    {
        var saved = await db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!await db.Stores.AnyAsync(x => x.Id == storeId && x.IsActive, ct))
                return false;

            var updated = await db.StoreRatings
                .Where(x => x.StoreId == storeId && x.VoterId == voterId && x.IsGuest == isGuest)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Score, score), ct);
            if (updated > 0)
            {
                await transaction.CommitAsync(ct);
                return true;
            }

            var rating = new StoreRating { StoreId = storeId, VoterId = voterId, IsGuest = isGuest, Score = score };
            db.StoreRatings.Add(rating);
            try
            {
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return true;
            }
            finally
            {
                db.Entry(rating).State = EntityState.Detached;
            }
        }, ct);

        return saved ? await GetAsync(storeId, ct) : null;
    }
}

public record StoreStatistics(int VisitCount, int ProductCount, double? AverageRating, int RatingCount);
