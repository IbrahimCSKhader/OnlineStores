using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.CustomerStore;

public class CustomerProfileInput
{
    [Required, MaxLength(100)] public string FirstName { get; set; } = "";
    [MaxLength(100)] public string LastName { get; set; } = "";
    [MaxLength(50)] public string? Phone { get; set; }
    [MaxLength(300)] public string? Address { get; set; }
    [MaxLength(100)] public string? City { get; set; }
    public bool HasSharedPhone { get; set; }
}

public class RecordCustomerPaymentDto
{
    [Required, StringLength(100, MinimumLength = 8)] public string RequestKey { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    [Required, MaxLength(80)] public string Method { get; set; } = "نقدي";
    [MaxLength(1000)] public string? Notes { get; set; }
    public bool IsRefund { get; set; }
    public List<PaymentAllocationInput> Allocations { get; set; } = new();
}
public class PaymentAllocationInput
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
}
public class AccountReasonDto
{
    [Required, MaxLength(1000)] public string Reason { get; set; } = "";
}
public class MergeCustomerDto : AccountReasonDto
{
    public Guid TargetCustomerId { get; set; }
}
public class ReassignCustomerDto : AccountReasonDto
{
    public Guid CustomerId { get; set; }
}
public record CustomerAccountOrder(Guid Id, string OrderNumber, DateTime CreatedAt, int Status, decimal TotalAmount, decimal Charge,
    decimal PaidAmount, decimal RemainingAmount, decimal Credit, string PaymentStatus);
public record CustomerStatementRow(Guid Id, DateTime Date, DateTime RecordedAt, string Kind, decimal Amount, decimal Balance,
    Guid? OrderId, string? OrderNumber, string? Method, string? Notes, Guid? RecordedBy, Guid? ReversesEntryId, List<CustomerStatementAllocation> Allocations);
public record CustomerStatementAllocation(Guid OrderId, string? OrderNumber, decimal Amount);
public record CustomerAccountDto(object Customer, decimal TotalOrders, decimal TotalPayments, decimal OutstandingBalance,
    decimal Credit, decimal UnallocatedCredit, List<CustomerAccountOrder> Orders, List<CustomerStatementRow> Statement);
