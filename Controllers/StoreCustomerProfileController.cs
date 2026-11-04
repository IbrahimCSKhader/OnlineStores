using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.StoreCustomerAuth;
using onlineStore.Models;
using onlineStore.Security;

namespace onlineStore.Controllers;

[ApiController]
[Route("api/store-customer-auth/store/{storeId:guid}/profile")]
[Authorize(Policy = "StoreCustomerOnly")]
public class StoreCustomerProfileController(AppDbContext db) : ControllerBase
{
    private async Task<StoreCustomer?> FindCustomer(Guid storeId, CancellationToken ct)
    {
        if (User.GetStoreCustomerStoreId() != storeId) return null;
        var customerId = User.GetStoreCustomerId();
        return await db.StoreCustomers.FirstOrDefaultAsync(c => c.Id == customerId && c.StoreId == storeId && c.IsActive && !c.IsDeleted && c.Store.IsActive && !c.Store.IsDeleted, ct);
    }

    private static object Profile(StoreCustomer c) => new { c.Id, c.StoreId, c.FirstName, c.LastName, c.Email, c.Phone, c.Address, c.City };

    [HttpGet]
    public async Task<IActionResult> Get(Guid storeId, CancellationToken ct)
    {
        var customer = await FindCustomer(storeId, ct);
        return customer == null ? Forbid() : Ok(Profile(customer));
    }

    [HttpPut]
    public async Task<IActionResult> Update(Guid storeId, UpdateStoreCustomerProfileDto dto, CancellationToken ct)
    {
        var customer = await FindCustomer(storeId, ct);
        if (customer == null) return Forbid();
        if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName)) return BadRequest();
        await onlineStore.Services.CustomerStore.CustomerIdentity.PreserveOrderSnapshotsAsync(db, customer, ct);
        customer.FirstName = dto.FirstName.Trim();
        customer.LastName = dto.LastName.Trim();
        customer.Phone = dto.Phone?.Trim();
        customer.Address = dto.Address?.Trim();
        customer.City = dto.City?.Trim();
        customer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(Profile(customer));
    }
}
