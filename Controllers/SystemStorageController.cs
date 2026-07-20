using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.Services.SystemStorage;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/system/storage")]
    [Authorize(Roles = "SuperAdmin")]
    public class SystemStorageController : ControllerBase
    {
        private readonly ISystemStorageService _systemStorageService;

        public SystemStorageController(ISystemStorageService systemStorageService)
        {
            _systemStorageService = systemStorageService;
        }

        [HttpPost("ensure-variant-folders")]
        public async Task<IActionResult> EnsureVariantFolders(CancellationToken cancellationToken)
        {
            var result = await _systemStorageService.EnsureVariantFoldersExistAsync(cancellationToken);
            return Ok(result);
        }

        [HttpPost("remove-empty-product-images-folders")]
        public async Task<IActionResult> RemoveEmptyProductImagesFolders(
            [FromQuery] bool dryRun = true,
            CancellationToken cancellationToken = default)
        {
            var result = await _systemStorageService.RemoveEmptyProductImagesFoldersAsync(
                dryRun,
                cancellationToken);

            return Ok(result);
        }
    }
}
