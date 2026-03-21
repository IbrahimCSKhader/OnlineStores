using Microsoft.EntityFrameworkCore;
using onlineStore.Models.Identity;

namespace onlineStore.Models.Cart
{
    [Index(nameof(UserId))]
    [Index(nameof(StoreId))]
    public class Cart : BaseEntity
    {
        public Guid UserId { get; set; }
        public AppUser User { get; set; }

        public Guid StoreId { get; set; }
        public Store Store { get; set; }

        // Navigation
        public ICollection<CartItem> Items { get; set; }
    }
}
