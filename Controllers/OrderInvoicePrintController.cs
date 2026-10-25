using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.Models.Orders;
using onlineStore.Security;

namespace onlineStore.Controllers;

[ApiController]
[Route("api/order-invoice-prints/store/{storeId:guid}")]
[Authorize(Roles = "SuperAdmin,StoreOwner")]
public class OrderInvoicePrintController(AppDbContext context, ICurrentUserService currentUser, IStoreAuthorizationService authorization) : ControllerBase
{
    public sealed record RecordPrintRequest(Guid RequestId);
    public sealed record PrintCountDto(Guid OrderId, int PrintCount, DateTime? LastPrintedAt);

    [HttpGet]
    public async Task<IActionResult> GetCounts(Guid storeId, CancellationToken cancellationToken)
    {
        if (!await CanManage(storeId, cancellationToken)) return Forbid();
        var counts = await context.Set<OrderInvoicePrint>().AsNoTracking()
            .Where(x => x.Order.StoreId == storeId && !x.Order.IsDeleted)
            .GroupBy(x => x.OrderId)
            .Select(g => new PrintCountDto(g.Key, g.Count(), g.Max(x => (DateTime?)x.CreatedAt)))
            .ToListAsync(cancellationToken);
        return Ok(counts);
    }

    [HttpPost("{orderId:guid}")]
    public async Task<IActionResult> Record(Guid storeId, Guid orderId, RecordPrintRequest request, CancellationToken cancellationToken)
    {
        if (!await CanManage(storeId, cancellationToken)) return Forbid();
        if (request.RequestId == Guid.Empty) return BadRequest(new { message = "RequestId is required." });
        if (!await context.Orders.AnyAsync(x => x.Id == orderId && x.StoreId == storeId, cancellationToken)) return NotFound();

        var existing = await context.Set<OrderInvoicePrint>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.RequestId, cancellationToken);
        if (existing != null && existing.OrderId != orderId) return Conflict(new { message = "RequestId belongs to another order." });
        if (existing == null)
        {
            var record = new OrderInvoicePrint { Id = request.RequestId, OrderId = orderId, PrintedByUserId = currentUser.UserId!.Value, CreatedAt = DateTime.UtcNow };
            context.Add(record);
            try { await context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // A simultaneous retry may already have saved exactly this attempt.
                context.Entry(record).State = EntityState.Detached;
                existing = await context.Set<OrderInvoicePrint>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.RequestId, cancellationToken);
                if (existing == null) throw;
                if (existing.OrderId != orderId) return Conflict(new { message = "RequestId belongs to another order." });
            }
        }
        var result = await context.Set<OrderInvoicePrint>().AsNoTracking().Where(x => x.OrderId == orderId)
            .GroupBy(x => x.OrderId).Select(g => new PrintCountDto(g.Key, g.Count(), g.Max(x => (DateTime?)x.CreatedAt)))
            .SingleAsync(cancellationToken);
        return Ok(result);
    }

    private Task<bool> CanManage(Guid storeId, CancellationToken ct) => currentUser.UserId is Guid userId
        ? authorization.CanManageStoreAsync(userId, storeId, ct)
        : Task.FromResult(false);
}
