using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using onlineStore.Security;
using onlineStore.Services.Store;

namespace onlineStore.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/Store/{storeId:guid}")]
public class StoreStatisticsController(StoreStatisticsService statistics, StoreFavoriteService customers) : ControllerBase
{
    [HttpGet("statistics")]
    public async Task<ActionResult<StoreStatistics>> Get(Guid storeId, CancellationToken ct)
    {
        var result = await statistics.GetAsync(storeId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("rating")]
    [EnableRateLimiting("store-ratings")]
    public async Task<ActionResult<StoreStatistics>> Rate(Guid storeId, RateStoreRequest request, CancellationToken ct)
    {
        if (Request.Headers.ContainsKey("Authorization") && User.Identity?.IsAuthenticated != true)
            return Unauthorized();

        var customerId = User.GetStoreCustomerId();
        var isGuest = !User.IsStoreCustomer();
        if (!isGuest && (customerId is null || User.GetStoreCustomerStoreId() != storeId
            || !await customers.IsRegisteredCustomerAsync(storeId, customerId.Value, ct)))
            return Forbid();

        var voterId = isGuest ? request.GuestId : customerId;
        if (voterId is null || voterId == Guid.Empty)
            return BadRequest();

        var result = await statistics.RateAsync(storeId, voterId.Value, isGuest, request.Score, ct);
        return result is null ? NotFound() : Ok(result);
    }
}

public record RateStoreRequest([Range(1, 5)] int Score, Guid? GuestId);
