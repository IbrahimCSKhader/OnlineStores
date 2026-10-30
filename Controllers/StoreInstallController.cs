using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.Services.Store;
using onlineStore.Utilities;
using Microsoft.Extensions.Caching.Memory;

namespace onlineStore.Controllers;

[ApiController]
[Route("api/store-install")]
public class StoreInstallController(IStoreService stores, IStorefrontOriginService origins) : ControllerBase
{
    [HttpGet("{slug}/icons/{size:int}.png")]
    [AllowAnonymous]
    [ResponseCache(Duration = 300)]
    public async Task<IActionResult> Icon(string slug, int size, [FromServices] IWebHostEnvironment environment, [FromServices] IMemoryCache cache)
    {
        if (size is not (192 or 512)) return BadRequest();
        var store = await stores.GetStoreBySlugAsync(slug);
        if (store is null || !store.IsActive) return NotFound();
        var source = StoreInstallIcon.FindLogo(store.LogoUrl, environment.ContentRootPath, environment.WebRootPath);
        var modified = source is null ? 0 : System.IO.File.GetLastWriteTimeUtc(source).Ticks;
        var bytes = cache.GetOrCreate($"store-install-icon:{slug}:{size}:{source}:{modified}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return StoreInstallIcon.Render(source, size);
        })!;
        return File(bytes, "image/png");
    }

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
