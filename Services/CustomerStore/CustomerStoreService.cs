using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.CustomerStore;

namespace onlineStore.Services.CustomerStore
{
    public class CustomerStoreService : ICustomerStoreService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CustomerStoreService> _logger;

        public CustomerStoreService(
            AppDbContext context,
            ILogger<CustomerStoreService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<CustomerListDto>> GetAllCustomersAsync()
        {
            return await (
                from user in _context.Users.AsNoTracking()
                join userRole in _context.UserRoles.AsNoTracking()
                    on user.Id equals userRole.UserId
                join role in _context.Roles.AsNoTracking()
                    on userRole.RoleId equals role.Id
                where role.Name == "Customer" && !user.IsDeleted
                orderby user.CreatedAt descending
                select new CustomerListDto
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    FullName = (user.FirstName + " " + user.LastName).Trim(),
                    Email = user.Email ?? string.Empty,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<List<CustomerStoreDto>> GetStoreCustomersAsync(Guid storeId)
        {
            return await _context.CustomerStores
                .AsNoTracking()
                .Where(x => x.StoreId == storeId)
                .Include(x => x.Customer)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new CustomerStoreDto
                {
                    Id = x.Id,
                    StoreId = x.StoreId,
                    CustomerId = x.CustomerId,
                    CustomerName = x.Customer.FirstName + " " + x.Customer.LastName,
                    CustomerEmail = x.Customer.Email!,
                    DiscountPercentage = x.DiscountPercentage,
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<CustomerStoreDto> CreateAsync(CreateCustomerStoreDto dto)
        {
            var storeExists = await _context.Stores
                .AsNoTracking()
                .AnyAsync(x => x.Id == dto.StoreId);

            if (!storeExists)
                throw new Exception("المتجر غير موجود");

            var customer = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == dto.CustomerId);

            if (customer == null)
                throw new Exception("الزبون غير موجود");

            var exists = await _context.CustomerStores
                .AnyAsync(x => x.StoreId == dto.StoreId && x.CustomerId == dto.CustomerId);

            if (exists)
                throw new Exception("هذا الزبون مضاف مسبقاً لهذا المتجر");

            var entity = new Models.CustomerStore
            {
                StoreId = dto.StoreId,
                CustomerId = dto.CustomerId,
                DiscountPercentage = dto.DiscountPercentage,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.CustomerStores.Add(entity);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "CustomerStore created for store {StoreId} and customer {CustomerId}",
                dto.StoreId,
                dto.CustomerId);

            return new CustomerStoreDto
            {
                Id = entity.Id,
                StoreId = entity.StoreId,
                CustomerId = entity.CustomerId,
                CustomerName = $"{customer.FirstName} {customer.LastName}",
                CustomerEmail = customer.Email ?? "",
                DiscountPercentage = entity.DiscountPercentage,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt
            };
        }

        public async Task<CustomerStoreDto?> UpdateAsync(Guid id, UpdateCustomerStoreDto dto)
        {
            var entity = await _context.CustomerStores
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null)
                return null;

            if (dto.DiscountPercentage.HasValue)
                entity.DiscountPercentage = dto.DiscountPercentage.Value;

            if (dto.IsActive.HasValue)
                entity.IsActive = dto.IsActive.Value;

            await _context.SaveChangesAsync();

            return new CustomerStoreDto
            {
                Id = entity.Id,
                StoreId = entity.StoreId,
                CustomerId = entity.CustomerId,
                CustomerName = $"{entity.Customer.FirstName} {entity.Customer.LastName}",
                CustomerEmail = entity.Customer.Email ?? "",
                DiscountPercentage = entity.DiscountPercentage,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt
            };
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var entity = await _context.CustomerStores
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null)
                return false;

            entity.IsDeleted = true;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<decimal?> GetCustomerDiscountAsync(Guid storeId, Guid customerId)
        {
            var row = await _context.CustomerStores
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.StoreId == storeId &&
                    x.CustomerId == customerId &&
                    x.IsActive);

            return row?.DiscountPercentage;
        }
    }
}
