using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using onlineStore.Models; 

namespace onlineStore.Models
{
    [Index(nameof(Slug), IsUnique = true)]
    public class Store : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; }

        [Required, MaxLength(100)]
        public string Slug { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public string? LogoUrl { get; set; }
        public string? CoverImageUrl { get; set; }

        [MaxLength(100)]
        public string? BusinessType { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<Category> Categories { get; set; }
        public ICollection<Section> Sections { get; set; }
    }
}