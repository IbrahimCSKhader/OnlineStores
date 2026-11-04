using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.CustomerStore;
using onlineStore.Models;
using onlineStore.Models.Enums;
using onlineStore.Models.Orders;
using onlineStore.Security;
using onlineStore.Utilities;

namespace onlineStore.Services.CustomerStore;

public class CustomerAccountService(AppDbContext db, ICurrentUserService actor, IStoreAuthorizationService authorization)
{
    public async Task AuthorizeAsync(Guid storeId, CancellationToken ct = default)
    {
        if (!actor.IsAuthenticated || !actor.UserId.HasValue ||
            !await authorization.CanManageStoreAsync(actor.UserId.Value, storeId, ct))
            throw new UnauthorizedAccessException("لا تملك صلاحية إدارة حسابات هذا المتجر");
    }

    private async Task<StoreCustomer> Customer(Guid storeId, Guid id, CancellationToken ct) =>
        await db.StoreCustomers.SingleOrDefaultAsync(x => x.StoreId == storeId && x.Id == id && x.MergedIntoCustomerId == null, ct)
        ?? throw new KeyNotFoundException("العميل غير موجود أو تم دمجه");

    private async Task<T> Write<T>(Guid storeId, Func<Task<T>> action, CancellationToken ct)
    {
        await AuthorizeAsync(storeId, ct);
        db.LedgerActorId = actor.UserId;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var value = await action();
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return value;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                throw;
            }
        });
    }

    public async Task<object> List(Guid storeId, string? search, int page, CancellationToken ct)
    {
        await AuthorizeAsync(storeId, ct);
        var query = db.StoreCustomers.AsNoTracking().Where(x => x.StoreId == storeId && x.MergedIntoCustomerId == null);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var phone = CustomerPhone.Normalize(term);
            query = query.Where(x => (x.FirstName + " " + x.LastName).Contains(term) ||
                (x.Phone != null && x.Phone.Contains(term)) || (phone != null && x.NormalizedPhone == phone));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Math.Max(page, 1) - 1) * 50).Take(50)
            .Select(x => new { x.Id, x.FirstName, x.LastName, x.Phone, x.Address, x.City, x.IsGuestProfile, x.HasSharedPhone, x.IsActive }).ToListAsync(ct);
        return new { items, total, page = Math.Max(page, 1), pageSize = 50 };
    }

    public Task<Guid> SaveProfile(Guid storeId, Guid? id, CustomerProfileInput input, CancellationToken ct) => Write(storeId, async () =>
    {
        if (string.IsNullOrWhiteSpace(input.FirstName)) throw new ArgumentException("اسم العميل مطلوب");
        var customer = id.HasValue ? await Customer(storeId, id.Value, ct) : new StoreCustomer
        { StoreId = storeId, IsGuestProfile = true, Email = $"guest-{Guid.NewGuid():N}@profiles.invalid", PasswordHash = "" };
        var previousIdentity = $"{customer.FirstName} {customer.LastName} | {customer.Phone}";
        if (id.HasValue) await CustomerIdentity.PreserveOrderSnapshotsAsync(db, customer, ct);
        customer.FirstName = input.FirstName.Trim(); customer.LastName = input.LastName.Trim();
        customer.Phone = input.Phone?.Trim(); customer.Address = input.Address?.Trim(); customer.City = input.City?.Trim();
        customer.HasSharedPhone = input.HasSharedPhone;
        if (!id.HasValue) db.StoreCustomers.Add(customer);
        db.CustomerAccountAudits.Add(new() { StoreId = storeId, ActorId = actor.UserId, Action = id.HasValue ? "ProfileUpdated" : "ProfileCreated",
            SourceId = customer.Id, TargetId = customer.Id, Reason = $"{previousIdentity} → {customer.FirstName} {customer.LastName} | {customer.Phone}" });
        return customer.Id;
    }, ct);

    public async Task<CustomerAccountDto> Account(Guid storeId, Guid id, CancellationToken ct)
    {
        await AuthorizeAsync(storeId, ct);
        // A consistent read prevents mixed balances while another request posts a payment.
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var customer = await Customer(storeId, id, ct);
            var orders = await db.Orders.IgnoreQueryFilters().AsNoTracking().Where(x => x.StoreId == storeId && x.AccountCustomerId == id).ToListAsync(ct);
            var entries = await db.CustomerLedgerEntries.AsNoTracking().Include(x => x.Allocations)
                .Where(x => x.StoreId == storeId && x.CustomerId == id).ToListAsync(ct);
            var allocations = entries.SelectMany(x => x.Allocations).GroupBy(x => x.OrderId).ToDictionary(x => x.Key, x => x.Sum(a => a.Amount));
            var orderRows = orders.OrderByDescending(x => x.CreatedAt).Select(o =>
            {
                var charge = Charge(o); var paid = allocations.GetValueOrDefault(o.Id);
                return new CustomerAccountOrder(o.Id, o.OrderNumber, o.CreatedAt, (int)o.Status, o.TotalAmount, charge, paid,
                    Math.Max(0, charge - paid), Math.Max(0, paid - charge), PaymentStatus(charge, paid));
            }).ToList();
            decimal running = 0;
            var statement = entries.OrderBy(x => x.OccurredAt).ThenBy(x => x.RecordedAt).ThenBy(x => x.Id).Select(x =>
                new CustomerStatementRow(x.Id, x.OccurredAt, x.RecordedAt, x.Kind, x.Amount, running += x.Amount, x.OrderId,
                    orders.FirstOrDefault(o => o.Id == x.OrderId)?.OrderNumber, x.Method, x.Notes, x.RecordedBy, x.ReversesEntryId,
                    x.Allocations.Select(a => new CustomerStatementAllocation(a.OrderId, orders.FirstOrDefault(o => o.Id == a.OrderId)?.OrderNumber, a.Amount)).ToList())).ToList();
            var charges = entries.Where(x => x.Kind == "OrderCharge").Sum(x => x.Amount);
            var payments = -entries.Where(x => x.Kind != "OrderCharge").Sum(x => x.Amount);
            var result = new CustomerAccountDto(new { customer.Id, customer.FirstName, customer.LastName, customer.Phone, customer.Address, customer.City,
                customer.IsGuestProfile, customer.HasSharedPhone }, charges, payments, Math.Max(0, charges - payments), Math.Max(0, payments - charges),
                Math.Max(0, payments - allocations.Values.Sum()), orderRows, statement);
            await tx.CommitAsync(ct);
            return result;
        });
    }

    public static decimal Charge(Models.Orders.Order order) => order.IsDeleted || order.Status is OrderStatus.Cancelled or OrderStatus.Refunded ? 0 : order.TotalAmount;
    public static string PaymentStatus(decimal charge, decimal paid) => paid > charge ? "credit" : paid == charge ? "paid" : paid > 0 ? "partial" : "unpaid";
    private static void Money(decimal amount)
    {
        if (amount <= 0 || amount > 9999999999999999.99m || decimal.Round(amount, 2) != amount)
            throw new ArgumentException("أدخل مبلغاً موجباً بمنزلتين عشريتين كحد أقصى");
    }

    public Task<Guid> Payment(Guid storeId, Guid id, RecordCustomerPaymentDto input, CancellationToken ct) => Write(storeId, async () =>
    {
        await Customer(storeId, id, ct);
        Money(input.Amount);
        if (string.IsNullOrWhiteSpace(input.RequestKey) || input.RequestKey.Length is < 8 or > 100 || string.IsNullOrWhiteSpace(input.Method) ||
            input.PaymentDate == default || input.PaymentDate > DateTime.UtcNow.AddMinutes(5) || input.Allocations.Count > 100)
            throw new ArgumentException("بيانات الدفعة غير صالحة");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { customerId = id, input }))));
        var previous = await db.CustomerLedgerEntries.SingleOrDefaultAsync(x => x.StoreId == storeId && x.RequestKey == input.RequestKey, ct);
        if (previous != null)
        {
            if (previous.RequestHash != hash) throw new ArgumentException("مفتاح الطلب مستخدم لدفعة مختلفة");
            return previous.Id;
        }
        if (input.Allocations.Select(x => x.OrderId).Distinct().Count() != input.Allocations.Count || input.Allocations.Sum(x => x.Amount) > input.Amount)
            throw new ArgumentException("توزيع الدفعة غير صالح");
        var entry = new CustomerLedgerEntry { StoreId = storeId, CustomerId = id, Kind = input.IsRefund ? "Refund" : "Payment",
            Amount = input.IsRefund ? input.Amount : -input.Amount, OccurredAt = input.PaymentDate.ToUniversalTime(), RecordedBy = actor.UserId,
            Method = input.Method.Trim(), Notes = input.Notes?.Trim(), RequestKey = input.RequestKey, RequestHash = hash };
        var allEntries = await db.CustomerLedgerEntries.Include(x => x.Allocations).Where(x => x.StoreId == storeId && x.CustomerId == id).ToListAsync(ct);
        var netPayments = -allEntries.Where(x => x.Kind != "OrderCharge").Sum(x => x.Amount);
        var allocated = allEntries.SelectMany(x => x.Allocations).Sum(x => x.Amount);
        if (input.IsRefund && (input.Amount > netPayments || input.Amount - input.Allocations.Sum(x => x.Amount) > netPayments - allocated))
            throw new ArgumentException("مبلغ الاسترداد يتجاوز المدفوعات المتاحة؛ حدد الطلبات لاسترداد مبالغها");
        foreach (var allocation in input.Allocations)
        {
            Money(allocation.Amount);
            var order = await db.Orders.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.StoreId == storeId && x.Id == allocation.OrderId && x.AccountCustomerId == id, ct)
                ?? throw new ArgumentException("الطلب لا ينتمي إلى هذا العميل");
            var paid = allEntries.SelectMany(x => x.Allocations).Where(x => x.OrderId == order.Id).Sum(x => x.Amount);
            if (input.IsRefund ? allocation.Amount > paid : allocation.Amount > Math.Max(0, Charge(order) - paid))
                throw new ArgumentException("المبلغ الموزع يتجاوز المبلغ المتاح على الطلب");
            var amount = input.IsRefund ? -allocation.Amount : allocation.Amount;
            entry.Allocations.Add(new() { OrderId = order.Id, Amount = amount });
            order.ReceivedAmount = paid + amount;
        }
        if (input.Allocations.Count == 1) entry.OrderId = input.Allocations[0].OrderId;
        db.CustomerLedgerEntries.Add(entry);
        return entry.Id;
    }, ct);

    public Task<Guid> Reverse(Guid storeId, Guid id, Guid entryId, string reason, CancellationToken ct) => Write(storeId, async () =>
    {
        await Customer(storeId, id, ct);
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("سبب التصحيح مطلوب");
        var original = await db.CustomerLedgerEntries.Include(x => x.Allocations).SingleOrDefaultAsync(x => x.StoreId == storeId && x.CustomerId == id && x.Id == entryId, ct)
            ?? throw new KeyNotFoundException("الحركة غير موجودة");
        var previous = await db.CustomerLedgerEntries.SingleOrDefaultAsync(x => x.ReversesEntryId == entryId, ct);
        if (previous != null) return previous.Id;
        if (original.Kind is "OrderCharge" or "Reversal") throw new ArgumentException("لا يمكن عكس هذه الحركة مباشرة");
        var movements = await db.CustomerLedgerEntries.Include(x => x.Allocations).Where(x => x.StoreId == storeId && x.CustomerId == id).ToListAsync(ct);
        var remainingPayments = -movements.Where(x => x.Kind != "OrderCharge").Sum(x => x.Amount) + original.Amount;
        var remainingAllocations = movements.SelectMany(x => x.Allocations).Sum(x => x.Amount) - original.Allocations.Sum(x => x.Amount);
        if (remainingPayments < remainingAllocations) throw new ArgumentException("اعكس توزيع الرصيد أو الاستردادات المرتبطة أولاً");
        var reversal = new CustomerLedgerEntry { StoreId = storeId, CustomerId = id, OrderId = original.OrderId,
            Kind = "Reversal", Amount = -original.Amount, RecordedBy = actor.UserId, Notes = reason, ReversesEntryId = original.Id };
        foreach (var allocation in original.Allocations)
        {
            var order = await db.Orders.IgnoreQueryFilters().SingleAsync(x => x.StoreId == storeId && x.Id == allocation.OrderId, ct);
            if ((order.ReceivedAmount ?? 0m) - allocation.Amount < 0) throw new ArgumentException("اعكس الاستردادات المرتبطة أولاً");
            order.ReceivedAmount = (order.ReceivedAmount ?? 0m) - allocation.Amount;
            reversal.Allocations.Add(new() { OrderId = order.Id, Amount = -allocation.Amount });
        }
        db.CustomerLedgerEntries.Add(reversal);
        return reversal.Id;
    }, ct);

    public Task<Guid> AllocateCredit(Guid storeId, Guid id, RecordCustomerPaymentDto input, CancellationToken ct) => Write(storeId, async () =>
    {
        await Customer(storeId, id, ct);
        if (input.Allocations.Count is < 1 or > 100 || input.Allocations.Select(x => x.OrderId).Distinct().Count() != input.Allocations.Count ||
            string.IsNullOrWhiteSpace(input.RequestKey) || input.RequestKey.Length is < 8 or > 100)
            throw new ArgumentException("توزيع الرصيد غير صالح");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { customerId = id, allocation = input }))));
        var previous = await db.CustomerLedgerEntries.SingleOrDefaultAsync(x => x.StoreId == storeId && x.RequestKey == input.RequestKey, ct);
        if (previous != null)
        {
            if (previous.RequestHash != hash) throw new ArgumentException("مفتاح الطلب مستخدم لحركة مختلفة");
            return previous.Id;
        }
        var entries = await db.CustomerLedgerEntries.Include(x => x.Allocations).Where(x => x.StoreId == storeId && x.CustomerId == id).ToListAsync(ct);
        var available = -entries.Where(x => x.Kind != "OrderCharge").Sum(x => x.Amount) - entries.SelectMany(x => x.Allocations).Sum(x => x.Amount);
        if (input.Allocations.Sum(x => x.Amount) > available) throw new ArgumentException("الرصيد غير الموزع غير كافٍ");
        var entry = new CustomerLedgerEntry { StoreId = storeId, CustomerId = id, Kind = "Allocation", Amount = 0,
            RecordedBy = actor.UserId, Notes = input.Notes, RequestKey = input.RequestKey, RequestHash = hash };
        foreach (var allocation in input.Allocations)
        {
            Money(allocation.Amount);
            var order = await db.Orders.SingleOrDefaultAsync(x => x.StoreId == storeId && x.AccountCustomerId == id && x.Id == allocation.OrderId, ct)
                ?? throw new ArgumentException("الطلب لا ينتمي إلى هذا العميل");
            var paid = entries.SelectMany(x => x.Allocations).Where(x => x.OrderId == order.Id).Sum(x => x.Amount);
            if (allocation.Amount > Math.Max(0, Charge(order) - paid)) throw new ArgumentException("المبلغ أكبر من المتبقي على الطلب");
            order.ReceivedAmount = paid + allocation.Amount;
            entry.Allocations.Add(new() { OrderId = order.Id, Amount = allocation.Amount });
        }
        db.CustomerLedgerEntries.Add(entry);
        return entry.Id;
    }, ct);

    public Task<int> Backfill(Guid storeId, CancellationToken ct) => Write(storeId, async () =>
    {
        var orders = await db.Orders.IgnoreQueryFilters().Where(x => x.StoreId == storeId && !x.AccountInitialized).OrderBy(x => x.CreatedAt).Take(200).ToListAsync(ct);
        foreach (var order in orders)
        {
            var customer = await CustomerIdentity.ResolveAsync(db, storeId, order.CustomerName, order.DeliveryPhone, order.StoreCustomerId, ct);
            order.AccountCustomerId = customer.Id;
            if (db.Entry(customer).State == EntityState.Added)
            {
                customer.Address = order.DeliveryAddress;
                customer.City = order.DeliveryCity;
            }
            // Save within this transaction so later historical rows can match the newly created profile.
            await db.SaveChangesAsync(ct);
        }
        db.CustomerAccountAudits.Add(new() { StoreId = storeId, ActorId = actor.UserId, Action = "HistoricalBackfill", Reason = $"{orders.Count} orders" });
        return orders.Count;
    }, ct);

    public Task<bool> Reassign(Guid storeId, Guid orderId, Guid customerId, string reason, CancellationToken ct) => Write(storeId, async () =>
    {
        await Customer(storeId, customerId, ct);
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("سبب التصحيح مطلوب");
        var order = await db.Orders.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.StoreId == storeId && x.Id == orderId, ct)
            ?? throw new KeyNotFoundException("الطلب غير موجود");
        if (order.AccountCustomerId == customerId) return true;
        // Multi-order receipts must first be reversed so unrelated accounts never inherit them.
        var entries = await db.CustomerLedgerEntries.Include(x => x.Allocations).Where(x => x.StoreId == storeId &&
            (x.OrderId == orderId || x.Allocations.Any(a => a.OrderId == orderId))).ToListAsync(ct);
        if (entries.Any(x => x.Allocations.Any(a => a.OrderId != orderId)))
            throw new ArgumentException("اعكس الدفعات الموزعة على عدة طلبات قبل نقل الطلب");
        var sourceId = order.AccountCustomerId;
        var affected = await db.CustomerLedgerEntries.Include(x => x.Allocations).Where(x => x.StoreId == storeId &&
            (x.CustomerId == sourceId || x.CustomerId == customerId)).ToListAsync(ct);
        var moving = entries.Select(x => x.Id).ToHashSet();
        foreach (var group in affected.GroupBy(x => moving.Contains(x.Id) ? customerId : x.CustomerId))
        {
            var payments = -group.Where(x => x.Kind != "OrderCharge").Sum(x => x.Amount);
            if (payments < group.SelectMany(x => x.Allocations).Sum(x => x.Amount))
                throw new ArgumentException("اعكس توزيع الرصيد أو الاستردادات المرتبطة قبل نقل الطلب");
        }
        db.CustomerAccountAudits.Add(new() { StoreId = storeId, ActorId = actor.UserId, Action = "OrderReassigned", SourceId = order.AccountCustomerId ?? Guid.Empty,
            TargetId = customerId, Reason = $"{orderId}: {reason}" });
        order.AccountCustomerId = customerId;
        foreach (var entry in entries) entry.CustomerId = customerId;
        return true;
    }, ct);

    public Task<bool> Merge(Guid storeId, Guid sourceId, Guid targetId, string reason, CancellationToken ct) => Write(storeId, async () =>
    {
        if (sourceId == targetId || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("اختر عميلاً آخر وأدخل سبب الدمج");
        var source = await Customer(storeId, sourceId, ct); await Customer(storeId, targetId, ct);
        var orders = await db.Orders.IgnoreQueryFilters().Where(x => x.StoreId == storeId && x.AccountCustomerId == sourceId).ToListAsync(ct);
        foreach (var order in orders) order.AccountCustomerId = targetId;
        foreach (var entry in await db.CustomerLedgerEntries.Where(x => x.StoreId == storeId && x.CustomerId == sourceId).ToListAsync(ct)) entry.CustomerId = targetId;
        foreach (var previous in await db.StoreCustomers.Where(x => x.StoreId == storeId && x.MergedIntoCustomerId == sourceId).ToListAsync(ct)) previous.MergedIntoCustomerId = targetId;
        source.MergedIntoCustomerId = targetId;
        db.CustomerAccountAudits.Add(new() { StoreId = storeId, ActorId = actor.UserId, Action = "CustomersMerged", SourceId = sourceId, TargetId = targetId, Reason = reason });
        return true;
    }, ct);
}
