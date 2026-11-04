using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.CustomerStore;
using onlineStore.Services.CustomerStore;

namespace onlineStore.Controllers;

[ApiController]
[Route("api/CustomerStore/store/{storeId:guid}/accounts")]
[Authorize(Roles = "SuperAdmin,StoreOwner")]
public class CustomerAccountController(CustomerAccountService service, AppDbContext db) : ControllerBase
{
    private async Task<IActionResult> Run(Func<Task<object>> action)
    {
        try { return Ok(await action()); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (DbUpdateException) { return Conflict(new { message = "تعارض مع طلب آخر؛ أعد المحاولة بنفس مفتاح الطلب" }); }
    }
    [HttpGet] public Task<IActionResult> List(Guid storeId, string? search, int page = 1, CancellationToken ct = default) => Run(() => service.List(storeId, search, page, ct));
    [HttpGet("{id:guid}")] public Task<IActionResult> Account(Guid storeId, Guid id, CancellationToken ct) => Run(async () => await service.Account(storeId, id, ct));
    [HttpPost] public Task<IActionResult> Create(Guid storeId, CustomerProfileInput dto, CancellationToken ct) => Run(async () => new { id = await service.SaveProfile(storeId, null, dto, ct) });
    [HttpPut("{id:guid}")] public Task<IActionResult> Update(Guid storeId, Guid id, CustomerProfileInput dto, CancellationToken ct) => Run(async () => new { id = await service.SaveProfile(storeId, id, dto, ct) });
    [HttpPost("{id:guid}/payments")] public Task<IActionResult> Payment(Guid storeId, Guid id, RecordCustomerPaymentDto dto, CancellationToken ct) => Run(async () => new { id = await service.Payment(storeId, id, dto, ct) });
    [HttpPost("{id:guid}/allocate-credit")] public Task<IActionResult> Allocate(Guid storeId, Guid id, RecordCustomerPaymentDto dto, CancellationToken ct) => Run(async () => new { id = await service.AllocateCredit(storeId, id, dto, ct) });
    [HttpPost("{id:guid}/entries/{entryId:guid}/reverse")] public Task<IActionResult> Reverse(Guid storeId, Guid id, Guid entryId, AccountReasonDto dto, CancellationToken ct) => Run(async () => new { id = await service.Reverse(storeId, id, entryId, dto.Reason, ct) });
    [HttpPost("backfill")] public Task<IActionResult> Backfill(Guid storeId, CancellationToken ct) => Run(async () => new { processed = await service.Backfill(storeId, ct), remaining = await db.Orders.IgnoreQueryFilters().CountAsync(x => x.StoreId == storeId && !x.AccountInitialized, ct) });
    [HttpPost("{id:guid}/merge")] public Task<IActionResult> Merge(Guid storeId, Guid id, MergeCustomerDto dto, CancellationToken ct) => Run(async () => new { success = await service.Merge(storeId, id, dto.TargetCustomerId, dto.Reason, ct) });
    [HttpPut("orders/{orderId:guid}/customer")] public Task<IActionResult> Reassign(Guid storeId, Guid orderId, ReassignCustomerDto dto, CancellationToken ct) => Run(async () => new { success = await service.Reassign(storeId, orderId, dto.CustomerId, dto.Reason, ct) });
    [HttpGet("audit")] public Task<IActionResult> Audit(Guid storeId, CancellationToken ct) => Run(async () =>
    {
        await service.AuthorizeAsync(storeId, ct);
        return await db.CustomerAccountAudits.AsNoTracking().Where(x => x.StoreId == storeId).OrderByDescending(x => x.RecordedAt).Take(200).ToListAsync(ct);
    });
}
