using onlineStore.DTOs.Section;

namespace onlineStore.Services.Section
{
    public interface ISectionService
    {
        Task<List<SectionDto>> GetStoreSectionsAsync(
            Guid storeId,
            CancellationToken cancellationToken = default);

        Task<SectionDto?> GetSectionByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        Task<SectionDto> CreateSectionAsync(
            CreateSectionDto dto,
            CancellationToken cancellationToken = default);

        Task<SectionDto?> UpdateSectionAsync(
            Guid id,
            UpdateSectionDto dto,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteSectionAsync(
            Guid id,
            CancellationToken cancellationToken = default);
    }
}