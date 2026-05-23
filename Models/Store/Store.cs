using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using onlineStore.Models.Identity;
using onlineStore.Models.Offers;
using onlineStore.Models.Subscriptions;

namespace onlineStore.Models
{
    [Index(nameof(Slug), IsUnique = true)]
    [Index(nameof(CustomDomain), IsUnique = true)]
    [Index(nameof(OwnerId))]
    public class Store : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(100)]
        public string Slug { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? CustomDomain { get; set; }

        public string? LogoUrl { get; set; }
        public string? CoverImageUrl { get; set; }

        [MaxLength(100)]
        public string? BusinessType { get; set; }

        public bool IsActive { get; set; } = true;

        public Guid OwnerId { get; set; }
        public AppUser Owner { get; set; } = null!;
        [MaxLength(30)]
        public string? WhatsAppNumber { get; set; }
        [MaxLength(100000)]
        public string? StoreStory { get; set; }

        [Required, MaxLength(1)]
        public string ThemeTemplate { get; set; } = StoreThemeTemplates.Default;
        public int VisitCount { get; set; } = 0;
        
        public ICollection<Category> Categories { get; set; } = new List<Category>();
        public ICollection<Section> Sections { get; set; } = new List<Section>();
        public ICollection<StoreContactAccount> ContactAccounts { get; set; } = new List<StoreContactAccount>();
        public ICollection<StoreSubscription> StoreSubscriptions { get; set; } = new List<StoreSubscription>();
        public ICollection<Offer> Offers { get; set; } = new List<Offer>();
    }
}
