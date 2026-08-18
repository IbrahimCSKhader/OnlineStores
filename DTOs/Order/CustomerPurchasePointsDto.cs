namespace onlineStore.DTOs.Order
{
    public class CustomerPurchasePointsDto
    {
        public Guid StoreCustomerId { get; set; }
        public Guid StoreId { get; set; }
        public int PurchasePoints { get; set; }
        public int PointsPerProduct { get; set; }
        public int TotalOrders { get; set; }
        public int TotalPurchasedItems { get; set; }
        public int RecentPointsEarned { get; set; }
        public DateTime? LastOrderAt { get; set; }
    }
}
