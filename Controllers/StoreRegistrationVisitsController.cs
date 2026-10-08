using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.Data;
using onlineStore.Models;

namespace onlineStore.Controllers;

[ApiController]
[Route("api/store-registration/visits")]
public sealed class StoreRegistrationVisitsController(AppDbContext context) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> RecordVisit(CancellationToken cancellationToken)
    {
        context.StoreRegistrationVisits.Add(new StoreRegistrationVisit());
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
