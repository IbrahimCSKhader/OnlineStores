using Microsoft.EntityFrameworkCore;
using onlineStore.Models.Identity;
using System.ComponentModel.DataAnnotations;

namespace onlineStore.Models
{
    [Index(nameof(StoreId), nameof(CustomerId), IsUnique = true)]
    public class CustomerStore : BaseEntity
    {
        public Guid StoreId { get; set; }
        public Store Store { get; set; } = null!;

        public Guid CustomerId { get; set; }
        public AppUser Customer { get; set; } = null!;

        [Range(0, 100)]
        public decimal DiscountPercentage { get; set; }

        public bool IsActive { get; set; } = true;
    }
}