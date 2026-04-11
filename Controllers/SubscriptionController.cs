using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Subscription;
using onlineStore.Services.Subscription;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/subscriptions")]
    [Authorize(Roles = "SuperAdmin,StoreOwner")]
    public class SubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;

        public SubscriptionController(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        [HttpGet("plans")]
        public async Task<IActionResult> GetPlans()
        {
            var plans = await _subscriptionService.GetAllPlansAsync();
            return Ok(plans);
        }

        [HttpGet("store/{storeId:guid}")]
        public async Task<IActionResult> GetStoreSubscription(Guid storeId)
        {
            var subscription = await _subscriptionService.GetActiveSubscriptionAsync(storeId);
            if (subscription == null)
                return NotFound(new { message = "لا يوجد اشتراك فعال" });

            return Ok(subscription);
        }

        [HttpPost("assign")]
        public async Task<IActionResult> Assign([FromBody] AssignSubscriptionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _subscriptionService.AssignPlanToStoreAsync(dto.StoreId, dto.PlanId);
            return Ok(result);
        }

        [HttpPut("change")]
        public async Task<IActionResult> Change([FromBody] ChangeSubscriptionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _subscriptionService.ChangePlanAsync(dto.StoreId, dto.NewPlanId);
            return Ok(result);
        }
    }
}
