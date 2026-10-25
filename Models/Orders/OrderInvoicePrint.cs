namespace onlineStore.Models.Orders;

// One record per browser print attempt. Id also makes network retries idempotent.
public class OrderInvoicePrint
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public Guid PrintedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
