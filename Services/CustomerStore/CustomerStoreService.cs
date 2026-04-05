using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.CustomerStore;
using onlineStore.Models;
using onlineStore.Services.StoreCustomerAuth;

namespace onlineStore.Services.CustomerStore
{
    public class CustomerStoreService : ICustomerStoreService
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<StoreCustomer> _passwordHasher;
        private readonly IStoreCustomerEmailWorkflowService _emailWorkflowService;
        private readonly ILogger<CustomerStoreService> _logger;

        public CustomerStoreService(
            AppDbContext context,
            IPasswordHasher<StoreCustomer> passwordHasher,
            IStoreCustomerEmailWorkflowService emailWorkflowService,
            ILogger<CustomerStoreService> logger)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _emailWorkflowService = emailWorkflowService;
            _logger = logger;
        }

        public async Task<List<CustomerListDto>> GetAllCustomersAsync()
        {
            return await _context.StoreCustomers
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new CustomerListDto
                {
                    Id = x.Id,
                    StoreId = x.StoreId,
                    FirstName = x.FirstName,
                    LastName = x.LastName,
                    FullName = (x.FirstName + " " + x.LastName).Trim(),
                    Email = x.Email,
                    Phone = x.Phone,
                    DiscountPercentage = x.DiscountPercentage,
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<List<CustomerStoreDto>> GetStoreCustomersAsync(Guid storeId)
        {
            return await _context.StoreCustomers
                .AsNoTracking()
                .Where(x => x.StoreId == storeId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new CustomerStoreDto
                {
                    Id = x.Id,
                    StoreId = x.StoreId,
                    FirstName = x.FirstName,
                    LastName = x.LastName,
                    FullName = (x.FirstName + " " + x.LastName).Trim(),
                    Email = x.Email,
                    Phone = x.Phone,
                    DiscountPercentage = x.DiscountPercentage,
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<CustomerStoreDto> CreateAsync(CreateCustomerStoreDto dto)
        {
            var store = await _context.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == dto.StoreId);

            if (store == null)
                throw new Exception("ط§ظ„ظ…طھط¬ط± ط؛ظٹط± ظ…ظˆط¬ظˆط¯");

            var normalizedEmail = NormalizeEmail(dto.Email);

            var exists = await _context.StoreCustomers
                .IgnoreQueryFilters()
                .AnyAsync(x => x.StoreId == dto.StoreId && x.Email == normalizedEmail);

            if (exists)
                throw new Exception("ط§ظ„ط¨ط±ظٹط¯ ط§ظ„ط¥ظ„ظƒطھط±ظˆظ†ظٹ ظ…ط³طھط®ط¯ظ… ظ…ط³ط¨ظ‚ط§ظ‹ ط¯ط§ط®ظ„ ظ‡ط°ط§ ط§ظ„ظ…طھط¬ط±");

            var entity = new StoreCustomer
            {
                StoreId = dto.StoreId,
                FirstName = NormalizeValue(dto.FirstName),
                LastName = NormalizeValue(dto.LastName),
                Email = normalizedEmail,
                Phone = NormalizeOptionalValue(dto.Phone),
                DiscountPercentage = dto.DiscountPercentage,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            entity.PasswordHash = _passwordHasher.HashPassword(entity, dto.Password);
            var verificationCode = _emailWorkflowService.PrepareEmailVerification(entity);

            _context.StoreCustomers.Add(entity);
            await _context.SaveChangesAsync();

            await _emailWorkflowService.SendEmailVerificationCodeAsync(
                entity,
                verificationCode,
                "store customer creation by owner");

            _logger.LogInformation(
                "Store customer created. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                dto.StoreId,
                entity.Id);

            return MapToDto(entity);
        }

        public async Task<CustomerStoreDto?> UpdateAsync(Guid id, UpdateCustomerStoreDto dto)
        {
            var entity = await _context.StoreCustomers
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null)
                return null;

            string? verificationCode = null;

            if (dto.FirstName != null)
                entity.FirstName = NormalizeValue(dto.FirstName);

            if (dto.LastName != null)
                entity.LastName = NormalizeValue(dto.LastName);

            if (dto.Email != null)
            {
                var normalizedEmail = NormalizeEmail(dto.Email);

                var emailExists = await _context.StoreCustomers
                    .IgnoreQueryFilters()
                    .AnyAsync(x => x.StoreId == entity.StoreId
                                && x.Email == normalizedEmail
                                && x.Id != entity.Id);

                if (emailExists)
                    throw new Exception("ط§ظ„ط¨ط±ظٹط¯ ط§ظ„ط¥ظ„ظƒطھط±ظˆظ†ظٹ ظ…ط³طھط®ط¯ظ… ظ…ط³ط¨ظ‚ط§ظ‹ ط¯ط§ط®ظ„ ظ‡ط°ط§ ط§ظ„ظ…طھط¬ط±");

                if (!string.Equals(entity.Email, normalizedEmail, StringComparison.Ordinal))
                {
                    entity.Email = normalizedEmail;
                    verificationCode = _emailWorkflowService.PrepareEmailVerification(entity);
                }
            }

            if (dto.Phone != null)
                entity.Phone = NormalizeOptionalValue(dto.Phone);

            if (dto.Password != null)
            {
                entity.PasswordHash = _passwordHasher.HashPassword(entity, dto.Password);
                _emailWorkflowService.ClearPasswordReset(entity);
            }

            if (dto.DiscountPercentage.HasValue)
                entity.DiscountPercentage = dto.DiscountPercentage.Value;

            if (dto.IsActive.HasValue)
                entity.IsActive = dto.IsActive.Value;

            await _context.SaveChangesAsync();

            if (verificationCode != null)
            {
                await _emailWorkflowService.SendEmailVerificationCodeAsync(
                    entity,
                    verificationCode,
                    "store customer email update by owner");
            }

            if (dto.Password != null)
            {
                await _emailWorkflowService.SendPasswordResetConfirmationAsync(
                    entity,
                    "store customer password update by owner");
            }

            return MapToDto(entity);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var entity = await _context.StoreCustomers
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null)
                return false;

            entity.IsDeleted = true;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<decimal?> GetCustomerDiscountAsync(Guid storeId, Guid storeCustomerId)
        {
            var row = await _context.StoreCustomers
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.StoreId == storeId &&
                    x.Id == storeCustomerId &&
                    x.IsActive);

            return row?.DiscountPercentage;
        }

        private static CustomerStoreDto MapToDto(StoreCustomer entity) => new()
        {
            Id = entity.Id,
            StoreId = entity.StoreId,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            FullName = $"{entity.FirstName} {entity.LastName}".Trim(),
            Email = entity.Email,
            Phone = entity.Phone,
            DiscountPercentage = entity.DiscountPercentage,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };

        private static string NormalizeEmail(string email) =>
            email.Trim().ToLowerInvariant();

        private static string NormalizeValue(string value) =>
            value.Trim();

        private static string? NormalizeOptionalValue(string? value)
        {
            var normalizedValue = value?.Trim();
            return string.IsNullOrWhiteSpace(normalizedValue) ? null : normalizedValue;
        }
    }
}
