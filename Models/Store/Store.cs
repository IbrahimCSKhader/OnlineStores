using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using onlineStore.Models.Identity;

namespace onlineStore.Models
{
    [Index(nameof(Slug), IsUnique = true)]
    [Index(nameof(OwnerId))]
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

        public Guid OwnerId { get; set; }
        public AppUser Owner { get; set; }
        [MaxLength(30)]
        public string? WhatsAppNumber { get; set; }

        [MaxLength(50)]
        public string? ThemeTemplate { get; set; } = "default";
        public int VisitCount { get; set; } = 0;
        
        public ICollection<Category> Categories { get; set; } = new List<Category>();
        public ICollection<Section> Sections { get; set; } = new List<Section>();
    }
}