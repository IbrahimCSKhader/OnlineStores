using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.Services.Store;
using onlineStore.Utilities;

namespace onlineStore.Controllers;

[ApiController]
[Route("api/store-install")]
public class StoreInstallController(IStoreService stores, IStorefrontOriginService origins) : ControllerBase
{
    [HttpGet("{slug}/manifest.webmanifest")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Manifest(string slug, [FromQuery] string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var launch) ||
            (launch.Scheme != "https" && !(launch.Scheme == "http" && launch.IsLoopback)) ||
            !string.IsNullOrEmpty(launch.UserInfo) ||
            !origins.IsAllowedOrigin(launch.GetLeftPart(UriPartial.Authority)))
            return BadRequest(new { message = "Invalid storefront URL." });

        var store = await stores.GetStoreBySlugAsync(slug);
        if (store is null || !store.IsActive) return NotFound();
        var customDomain = !origins.IsPlatformHost(launch.Host);
        if (customDomain && StoreDomainNormalizer.NormalizeHost(store.CustomDomain) != StoreDomainNormalizer.NormalizeHost(launch.Host))
            return BadRequest(new { message = "Domain does not belong to this store." });

        try
        {
            var assetBase = new Uri($"{Request.Scheme}://{Request.Host}{Request.PathBase}/");
            return new JsonResult(StoreInstallManifest.Create(store, launch, customDomain, assetBase))
            {
                ContentType = "application/manifest+json"
            };
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "Invalid public store launch URL." });
        }
    }
}
