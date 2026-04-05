using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
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
    }
}
