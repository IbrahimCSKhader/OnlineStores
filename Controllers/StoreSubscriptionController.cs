using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Subscription;
using onlineStore.Services.Subscription;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/store-subscriptions")]
    [Authorize(Roles = "SuperAdmin,StoreOwner")]
    public class StoreSubscriptionController : ControllerBase
    {
        private readonly IStoreSubscriptionService _storeSubscriptionService;

        public StoreSubscriptionController(IStoreSubscriptionService storeSubscriptionService)
        {
            _storeSubscriptionService = storeSubscriptionService;
        }

        [HttpGet("store/{storeId:guid}")]
        public async Task<IActionResult> GetStoreSubscriptions(Guid storeId)
        {
            var subscriptions = await _storeSubscriptionService.GetStoreSubscriptionsAsync(storeId);
            return Ok(subscriptions);
        }

        [HttpPost("assign")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Assign([FromBody] AssignStoreSubscriptionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var assigned = await _storeSubscriptionService.AssignStoreSubscriptionAsync(dto);
            return Ok(assigned);
        }

        [HttpPut("{id:guid}/change-plan")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ChangePlan(Guid id, [FromBody] ChangeStoreSubscriptionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var updated = await _storeSubscriptionService.ChangeStoreSubscriptionAsync(id, dto);
            if (updated == null)
                return NotFound(new { message = "Store subscription not found." });

            return Ok(updated);
        }

        [HttpPut("{id:guid}/cancel")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var cancelled = await _storeSubscriptionService.CancelStoreSubscriptionAsync(id, "Cancelled via API");
            if (!cancelled)
                return NotFound(new { message = "Store subscription not found." });

            return Ok(new { message = "Store subscription cancelled successfully." });
        }

        [HttpGet("store/{storeId:guid}/active-plan")]
        public async Task<IActionResult> GetActivePlan(Guid storeId)
        {
            var active = await _storeSubscriptionService.GetActiveSubscriptionForStoreAsync(storeId);
            if (active == null)
                return NotFound(new { message = "No active subscription found for this store." });

            return Ok(active);
        }
    }
}
