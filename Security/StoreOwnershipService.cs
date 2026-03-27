using Microsoft.EntityFrameworkCore;
using onlineStore.Data;

namespace onlineStore.Security
{
    public class StoreOwnershipService : IStoreOwnershipService
    {
        private readonly AppDbContext _context;

        public StoreOwnershipService(AppDbContext context)
        {
            _context = context;
        }

        public Task<bool> UserOwnsStoreAsync(Guid storeId, Guid userId, CancellationToken cancellationToken = default)
        {
            return _context.Stores
                .AsNoTracking()
                .AnyAsync(s => s.Id == storeId && s.OwnerId == userId, cancellationToken);
        }
    }
}