using Microsoft.EntityFrameworkCore;
namespace onlineStore.Models.CartModels
{
    [Index(nameof(StoreCustomerId))]
    [Index(nameof(StoreId))]
    public class ShoppingCart : BaseEntity
    {
        public Guid StoreCustomerId { get; set; }
        public StoreCustomer StoreCustomer { get; set; } = null!;

        public Guid StoreId { get; set; }
        public Store Store { get; set; } = null!;

        // Navigation
        public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
    }
}
