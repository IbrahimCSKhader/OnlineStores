using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Store;

public class CatalogTranslationDto
{
    [Required, RegularExpression("^[AEI]$")]
    public string Language { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? Name { get; set; }
    [MaxLength(2000)]
    public string? Description { get; set; }
    [MaxLength(500)]
    public string? ShortDescription { get; set; }
}
