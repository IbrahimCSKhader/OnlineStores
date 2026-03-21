using Microsoft.EntityFrameworkCore;
using onlineStore.Models.Identity;
using System.ComponentModel.DataAnnotations;

namespace onlineStore.Models.Reviews
{
    [Index(nameof(ProductId))]
    [Index(nameof(UserId))]
    [Index(nameof(StoreId))]
    public class Review : BaseEntity
    {
        [Range(1, 5)]
        public int Rating { get; set; } // 1 to 5 stars

        [MaxLength(1000)]
        public string? Comment { get; set; }

        public bool IsApproved { get; set; } = false; // صاحب المتجر يوافق قبل النشر

        public Guid ProductId { get; set; }
        public Product Product { get; set; }

        public Guid UserId { get; set; }
        public AppUser User { get; set; }

        public Guid StoreId { get; set; }
        public Store Store { get; set; }
    }
}
