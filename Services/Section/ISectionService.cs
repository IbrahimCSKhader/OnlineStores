using onlineStore.DTOs.Section;

namespace onlineStore.Services.Section
{
    public interface ISectionService
    {
              Task<List<SectionDto>> GetStoreSectionsAsync(Guid storeId);
    Task<SectionDto?> GetSectionByIdAsync(Guid id);
    Task<SectionDto> CreateSectionAsync(CreateSectionDto dto);
    Task<SectionDto?> UpdateSectionAsync(Guid id, UpdateSectionDto dto);
    Task<bool> DeleteSectionAsync(Guid id);
    
    }
}
