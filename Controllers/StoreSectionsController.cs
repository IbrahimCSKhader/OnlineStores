using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Store;
using onlineStore.Models;
using onlineStore.Security;
using onlineStore.Utilities;
using System.Text.RegularExpressions;

namespace onlineStore.Controllers;
[ApiController]
[Route("api/Store/{storeId:guid}/sections-settings")]
[Authorize(Roles = "SuperAdmin,StoreOwner")]
public class StoreSectionsController(AppDbContext db, ICurrentUserService user, IStoreOwnershipService ownership, IWebHostEnvironment env) : ControllerBase
{
    private async Task<bool> Allowed(Guid id, CancellationToken ct) => user.IsSuperAdmin ||
        (user.IsStoreOwner && user.UserId.HasValue && await ownership.UserOwnsStoreAsync(id, user.UserId.Value, ct));
    [HttpGet]
    public async Task<IActionResult> Get(Guid storeId, CancellationToken ct)
    {
        if (!await Allowed(storeId, ct)) return Forbid();
        if (!await db.Stores.AnyAsync(s => s.Id == storeId, ct)) return NotFound();
        var row = await db.Set<StoreAppearance>().AsNoTracking().SingleOrDefaultAsync(s => s.StoreId == storeId, ct);
        return Ok(StoreAppearancePolicy.Read(row?.OverridesJson).Sections ?? new());
    }
    [HttpPut]
    [RequestSizeLimit(262144)]
    public async Task<IActionResult> Put(Guid storeId, StoreSectionsDto dto, CancellationToken ct)
    {
        if (!await Allowed(storeId, ct)) return Forbid();
        if (!await db.Stores.AnyAsync(s => s.Id == storeId, ct)) return NotFound();
        if (dto.Locations == null || dto.Locations.Count > 20 || dto.Locations.Any(branch =>
            branch == null || string.IsNullOrWhiteSpace(branch.Name) || branch.Name.Length > 180 ||
            string.IsNullOrWhiteSpace(branch.Address) || branch.Address.Length > 500 ||
            branch.Phone == null || !Regex.IsMatch(branch.Phone, @"^\+?[0-9 -]{0,30}$") ||
            branch.WhatsApp == null || !Regex.IsMatch(branch.WhatsApp, @"^\+?[0-9]{0,15}$")))
            return BadRequest("Invalid store locations.");
        bool Texts(Dictionary<string,string>? values) => values != null && values.Count <= 3 &&
            values.All(p => new[] { "A", "E", "I" }.Contains(p.Key) && p.Value != null && p.Value.Length <= 180);
        if (!Texts(dto.OffersHeading) || dto.Offers == null || dto.Offers.Count > 100) return BadRequest("Invalid section settings.");
        foreach (var item in dto.Offers.Values)
        {
            if (item == null || !Texts(item.Title) || !Texts(item.Badge) || item.Images == null || item.Images.Count > 12 ||
                item.Images.Any(url => string.IsNullOrWhiteSpace(url) || url.Length > 2000 ||
                    !(url.StartsWith($"/wwwroot/uploads/stores/{storeId}/") || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https"))) ||
                (!string.IsNullOrEmpty(item.TitleColor) && !Regex.IsMatch(item.TitleColor, "^#[0-9a-fA-F]{6}$")))
                return BadRequest("Invalid offer images, translations or title color.");
        }
        var ids = dto.Offers.Keys.ToList();
        if (dto.PremiumOfferId.HasValue) ids.Add(dto.PremiumOfferId.Value);
        if (await db.Set<onlineStore.Models.Offers.Offer>().CountAsync(o => o.StoreId == storeId && ids.Contains(o.Id), ct) != ids.Distinct().Count())
            return BadRequest("Offers must belong to this store.");
        var row = await db.Set<StoreAppearance>().SingleOrDefaultAsync(s => s.StoreId == storeId, ct);
        if (row == null) { row = new() { StoreId = storeId }; db.Add(row); }
        var appearance = StoreAppearancePolicy.Read(row.OverridesJson);
        appearance.Sections = dto;
        var json = StoreAppearancePolicy.Serialize(appearance);
        if (json.Length > 16000) return BadRequest(new { message = "حجم إعدادات الأقسام كبير؛ قلل الصور أو النصوص قبل الحفظ." });
        row.OverridesJson = json;
        await db.SaveChangesAsync(ct);
        return Ok(dto);
    }
    [HttpPost("images")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid storeId, IFormFile image, CancellationToken ct)
    {
        if (!await Allowed(storeId, ct)) return Forbid();
        if (!await db.Stores.AnyAsync(s => s.Id == storeId, ct)) return NotFound();
        try { return Ok(new { url = (await CategoryImageStorage.SaveAsync(image, env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), storeId, ct)).Url }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
