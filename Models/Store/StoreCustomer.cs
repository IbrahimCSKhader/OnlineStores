using Microsoft.EntityFrameworkCore;
using onlineStore.Models.CartModels;
using onlineStore.Models.Orders;
using onlineStore.Models.Reviews;
using System.ComponentModel.DataAnnotations;

namespace onlineStore.Models
{
    [Index(nameof(StoreId), nameof(Email), IsUnique = true)]
    [Index(nameof(StoreId))]
    public class StoreCustomer : BaseEntity
    {
        public Guid StoreId { get; set; }
        public Store Store { get; set; } = null!;

        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        public bool EmailConfirmed { get; set; }

        [MaxLength(128)]
        public string? EmailVerificationCodeHash { get; set; }

        public DateTime? EmailVerificationCodeExpiresAt { get; set; }

        [MaxLength(128)]
        public string? PasswordResetCodeHash { get; set; }

        public DateTime? PasswordResetCodeExpiresAt { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Range(0, 100)]
        public decimal DiscountPercentage { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<ShoppingCart> Carts { get; set; } = new List<ShoppingCart>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
