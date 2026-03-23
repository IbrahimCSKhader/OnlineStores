// DTOs/Section/UpdateSectionDto.cs
using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Section
{
    public class UpdateSectionDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public string? ImageUrl { get; set; }
        public int? DisplayOrder { get; set; }
        public bool? IsActive { get; set; }
    }
}