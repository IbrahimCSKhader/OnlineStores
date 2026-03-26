using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using System.ComponentModel.DataAnnotations;
using onlineStore.Models.Orders;
using onlineStore.Models.Reviews;
using onlineStore.Models.CartModels;
namespace onlineStore.Models.Identity
{
    // Models/Identity/AppUser.cs
    public class AppUser : IdentityUser<Guid>
    {
        [MaxLength(100)]
        public string FirstName { get; set; }

        [MaxLength(100)]
        public string LastName { get; set; }

        public string? AvatarUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;

        // Navigation
        public ICollection<Order> Orders { get; set; }
        public ICollection<Review> Reviews { get; set; }
        public ICollection<ShoppingCart> Carts { get; set; }
    }
}
