using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace onlineStore.Models.Orders;

// Signed account movement: charges/refunds are positive, receipts are negative.
// Corrections append a reversal; posted entries are never edited or deleted.
public class CustomerLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public Guid CustomerId { get; set; }
    public StoreCustomer Customer { get; set; } = null!;
    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }
    [MaxLength(30)] public string Kind { get; set; } = "Payment";
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public Guid? RecordedBy { get; set; }
    [MaxLength(80)] public string? Method { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    [MaxLength(100)] public string? RequestKey { get; set; }
    [MaxLength(64)] public string? RequestHash { get; set; }
    public Guid? ReversesEntryId { get; set; }
    public List<CustomerPaymentAllocation> Allocations { get; set; } = new();
}

public class CustomerPaymentAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EntryId { get; set; }
    public CustomerLedgerEntry Entry { get; set; } = null!;
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    // Positive = money received for this order, negative = reversal/refund.
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
}

public class CustomerAccountAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public Guid? ActorId { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(40)] public string Action { get; set; } = "";
    public Guid SourceId { get; set; }
    public Guid TargetId { get; set; }
    [MaxLength(1000)] public string Reason { get; set; } = "";
}
