using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Store;
using onlineStore.Models;
using onlineStore.Utilities;

namespace onlineStore.Controllers;

[ApiController]
[Route("api/Store/{storeId:guid}/appearance")]
[Authorize(Roles = "SuperAdmin")]
public class StoreAppearanceController(AppDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid storeId, CancellationToken ct)
    {
        if (!await context.Stores.AnyAsync(x => x.Id == storeId, ct)) return NotFound();
        var row = await context.Set<StoreAppearance>().AsNoTracking().SingleOrDefaultAsync(x => x.StoreId == storeId, ct);
        return Ok(StoreAppearancePolicy.Read(row?.OverridesJson));
    }

    // PUT replaces sparse overrides; omitting a property resets it to its template default.
    [HttpPut]
    [RequestSizeLimit(16000)]
    public async Task<IActionResult> Put(Guid storeId, StoreAppearanceDto dto, CancellationToken ct)
    {
        StoreAppearanceDto validated;
        try { validated = StoreAppearancePolicy.Validate(dto); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        if (!await context.Stores.AnyAsync(x => x.Id == storeId, ct)) return NotFound();
        var row = await context.Set<StoreAppearance>().SingleOrDefaultAsync(x => x.StoreId == storeId, ct);
        if (validated.Light.Count + validated.Dark.Count == 0)
        {
            if (row != null) context.Remove(row);
        }
        else
        {
            if (row == null) { row = new() { StoreId = storeId }; context.Add(row); }
            row.OverridesJson = StoreAppearancePolicy.Serialize(validated);
        }
        await context.SaveChangesAsync(ct);
        return Ok(validated);
    }

    [HttpDelete]
    public Task<IActionResult> Reset(Guid storeId, CancellationToken ct) => Put(storeId, new(), ct);

    [HttpDelete("{mode}")]
    public async Task<IActionResult> ResetMode(Guid storeId, string mode, CancellationToken ct)
    {
        if (mode is not ("light" or "dark")) return BadRequest();
        if (!await context.Stores.AnyAsync(x => x.Id == storeId, ct)) return NotFound();
        var row = await context.Set<StoreAppearance>().SingleOrDefaultAsync(x => x.StoreId == storeId, ct);
        var dto = StoreAppearancePolicy.Read(row?.OverridesJson);
        if (mode == "light") dto.Light.Clear(); else dto.Dark.Clear();
        return await Put(storeId, dto, ct);
    }

    [HttpDelete("{mode}/{property}")]
    public async Task<IActionResult> ResetProperty(Guid storeId, string mode, string property, CancellationToken ct)
    {
        if (mode is not ("light" or "dark") ||
            !StoreAppearancePolicy.Colors.Concat(StoreAppearancePolicy.Fonts).Concat(StoreAppearancePolicy.Sizes).Contains(property))
            return BadRequest();
        if (!await context.Stores.AnyAsync(x => x.Id == storeId, ct)) return NotFound();
        var row = await context.Set<StoreAppearance>().SingleOrDefaultAsync(x => x.StoreId == storeId, ct);
        var dto = StoreAppearancePolicy.Read(row?.OverridesJson);
        (mode == "light" ? dto.Light : dto.Dark).Remove(property);
        return await Put(storeId, dto, ct);
    }
}
