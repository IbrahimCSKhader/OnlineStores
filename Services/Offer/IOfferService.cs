using onlineStore.DTOs.Offer;

namespace onlineStore.Services.Offer
{
    public interface IOfferService
    {
        Task<List<OfferDto>> GetStoreOffersAsync(Guid storeId);
        Task<List<OfferDto>> GetActiveOffersForStoreAsync(Guid storeId);
        Task<OfferDto?> GetOfferByIdAsync(Guid id);
        Task<OfferDto> CreateOfferAsync(CreateOfferDto dto);
        Task<OfferDto?> UpdateOfferAsync(Guid id, UpdateOfferDto dto);
        Task<bool> DeleteOfferAsync(Guid id);
        Task ValidateOfferUsageAsync(Guid storeId);
    }
}
