using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.Security;
using onlineStore.Services.Store;
using System.ComponentModel.DataAnnotations;

namespace onlineStore.Controllers;

[ApiController]
[Authorize(Policy = "StoreCustomerOnly")]
[Route("api/Store/{storeId:guid}/favorites")]
public class StoreFavoritesController(StoreFavoriteService favorites) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid storeId, CancellationToken ct)
    {
        var customerId = await GetCustomerAsync(storeId, ct);
        return customerId is Guid id ? Ok(await favorites.GetAsync(storeId, id, ct)) : Forbid();
    }

    [HttpPut("{productId:guid}")]
    public async Task<IActionResult> Add(Guid storeId, Guid productId, CancellationToken ct)
    {
        var customerId = await GetCustomerAsync(storeId, ct);
        if (customerId is not Guid id)
            return Forbid();
        return await favorites.AddAsync(storeId, id, productId, ct) ? NoContent() : NotFound();
    }

    [HttpDelete("{productId:guid}")]
    public async Task<IActionResult> Remove(Guid storeId, Guid productId, CancellationToken ct)
    {
        var customerId = await GetCustomerAsync(storeId, ct);
        if (customerId is not Guid id)
            return Forbid();
        await favorites.RemoveAsync(storeId, id, productId, ct);
        return NoContent();
    }

    [HttpPost("merge")]
    public async Task<IActionResult> Merge(Guid storeId, MergeFavoritesRequest request, CancellationToken ct)
    {
        var customerId = await GetCustomerAsync(storeId, ct);
        return customerId is Guid id
            ? Ok(await favorites.MergeAsync(storeId, id, request.ProductIds, ct))
            : Forbid();
    }

    private async Task<Guid?> GetCustomerAsync(Guid storeId, CancellationToken ct) =>
        User.GetStoreCustomerStoreId() == storeId && User.GetStoreCustomerId() is Guid customerId
            && await favorites.IsRegisteredCustomerAsync(storeId, customerId, ct)
            ? customerId
            : null;
}

public record MergeFavoritesRequest([Required, MaxLength(500)] Guid[] ProductIds);
