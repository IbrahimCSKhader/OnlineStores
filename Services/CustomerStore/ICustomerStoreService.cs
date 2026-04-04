using onlineStore.DTOs.CustomerStore;

namespace onlineStore.Services.CustomerStore
{
    public interface ICustomerStoreService
    {
        Task<List<CustomerListDto>> GetAllCustomersAsync();
        Task<List<CustomerStoreDto>> GetStoreCustomersAsync(Guid storeId);
        Task<CustomerStoreDto> CreateAsync(CreateCustomerStoreDto dto);
        Task<CustomerStoreDto?> UpdateAsync(Guid id, UpdateCustomerStoreDto dto);
        Task<bool> DeleteAsync(Guid id);

        Task<decimal?> GetCustomerDiscountAsync(Guid storeId, Guid customerId);
    }
}
