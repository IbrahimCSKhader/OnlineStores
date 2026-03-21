using Microsoft.EntityFrameworkCore;
using onlineStore.Models.Identity;
using System.ComponentModel.DataAnnotations;

namespace onlineStore.Models.Notifications
{
    [Index(nameof(UserId))]
    [Index(nameof(StoreId))]
    public class Notification : BaseEntity
    {
        [Required, MaxLength(200)]
        public string Title { get; set; }

        [MaxLength(1000)]
        public string? Message { get; set; }

        public bool IsRead { get; set; } = false;
        public string? Link { get; set; } // e.g. "/orders/ORD-001"

        public Guid UserId { get; set; } // صاحب المتجر أو الزبون
        public AppUser User { get; set; }

        public Guid? StoreId { get; set; }
        public Store? Store { get; set; }
    }
}
