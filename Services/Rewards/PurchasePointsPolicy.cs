using onlineStore.Models.CartModels;
using onlineStore.Models.Orders;

namespace onlineStore.Services.Rewards
{
    public static class PurchasePointsPolicy
    {
        public const int PointsPerProduct = 5;

        public static int CalculatePoints(IEnumerable<int> quantities)
        {
            if (quantities == null)
                return 0;

            return quantities
                .Where(quantity => quantity > 0)
                .Sum(quantity => quantity * PointsPerProduct);
        }

        public static int CalculatePointsFromCartItems(IEnumerable<CartItem> items) =>
            CalculatePoints(items?.Select(item => item.Quantity) ?? []);

        public static int CalculatePointsFromOrderItems(IEnumerable<OrderItem> items) =>
            CalculatePoints(items?.Select(item => item.Quantity) ?? []);
    }
}
