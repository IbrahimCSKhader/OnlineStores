using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.SuperAdminDashboard;
using onlineStore.Models.Identity;
using onlineStore.Services.SuperAdminDashboard;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/super-admin-dashboard")]
    [Authorize(Roles = "SuperAdmin")]
    public class SuperAdminDashboardController : ControllerBase
    {
        private readonly ISuperAdminDashboardService _superAdminDashboardService;

        public SuperAdminDashboardController(ISuperAdminDashboardService superAdminDashboardService)
        {
            _superAdminDashboardService = superAdminDashboardService;
        }

        [HttpGet("private-access")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> VerifyPrivateAccess(
            [FromServices] IConfiguration configuration,
            [FromServices] UserManager<AppUser> userManager)
        {
            var allowedEmail = configuration["SecretAdmin:Email"]?.Trim();
            if (string.IsNullOrWhiteSpace(allowedEmail))
                return StatusCode(503, new { message = "لم يتم إعداد حساب الدخول الخاص بعد." });

            var user = await userManager.GetUserAsync(User);
            if (user == null || !user.IsActive || user.IsDeleted ||
                !string.Equals(user.NormalizedEmail, userManager.NormalizeEmail(allowedEmail), StringComparison.Ordinal) ||
                !await userManager.IsInRoleAsync(user, "SuperAdmin"))
                return Forbid();

            return NoContent();
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var data = await _superAdminDashboardService.GetSummaryAsync();
            return Ok(data);
        }

        [HttpGet("owners")]
        public async Task<IActionResult> GetOwners()
        {
            var data = await _superAdminDashboardService.GetStoreOwnersAsync();
            return Ok(data);
        }

        [HttpGet("owners/{ownerId:guid}")]
        public async Task<IActionResult> GetOwnerDetails(Guid ownerId)
        {
            var data = await _superAdminDashboardService.GetStoreOwnerDetailsAsync(ownerId);
            if (data == null)
                return NotFound(new { message = "owner not found" });

            return Ok(data);
        }

        [HttpPut("owners/{ownerId:guid}/status")]
        public async Task<IActionResult> UpdateOwnerStatus(
            Guid ownerId,
            [FromBody] SuperAdminSetActivationStatusDto dto)
        {
            var updated = await _superAdminDashboardService.SetStoreOwnerStatusAsync(ownerId, dto.IsActive);
            if (!updated)
                return NotFound(new { message = "owner not found" });

            return Ok(new { message = "owner status updated successfully" });
        }

        [HttpGet("stores")]
        public async Task<IActionResult> GetStores()
        {
            var data = await _superAdminDashboardService.GetStoresAsync();
            return Ok(data);
        }

        [HttpGet("stores/{storeId:guid}")]
        public async Task<IActionResult> GetStoreDetails(Guid storeId)
        {
            var data = await _superAdminDashboardService.GetStoreDetailsAsync(storeId);
            if (data == null)
                return NotFound(new { message = "store not found" });

            return Ok(data);
        }

        [HttpPut("stores/{storeId:guid}/status")]
        public async Task<IActionResult> UpdateStoreStatus(
            Guid storeId,
            [FromBody] SuperAdminSetActivationStatusDto dto)
        {
            var updated = await _superAdminDashboardService.SetStoreStatusAsync(storeId, dto.IsActive);
            if (!updated)
                return NotFound(new { message = "store not found" });

            return Ok(new { message = "store status updated successfully" });
        }

        [HttpGet("stores/{storeId:guid}/customers")]
        public async Task<IActionResult> GetStoreCustomers(Guid storeId)
        {
            var data = await _superAdminDashboardService.GetStoreCustomersAsync(storeId);
            if (data == null)
                return NotFound(new { message = "store not found" });

            return Ok(data);
        }
    }
}
