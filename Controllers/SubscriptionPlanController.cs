using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Subscription;
using onlineStore.Services.Subscription;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/subscription-plans")]
    [Authorize(Roles = "SuperAdmin")]
    public class SubscriptionPlanController : ControllerBase
    {
        private readonly ISubscriptionPlanService _subscriptionPlanService;

        public SubscriptionPlanController(ISubscriptionPlanService subscriptionPlanService)
        {
            _subscriptionPlanService = subscriptionPlanService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var plans = await _subscriptionPlanService.GetAllAsync();
            return Ok(plans);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var plan = await _subscriptionPlanService.GetByIdAsync(id);
            if (plan == null)
                return NotFound(new { message = "Subscription plan not found." });

            return Ok(plan);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSubscriptionPlanDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var created = await _subscriptionPlanService.CreateAsync(dto);
            return Ok(created);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSubscriptionPlanDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var updated = await _subscriptionPlanService.UpdateAsync(id, dto);
            if (updated == null)
                return NotFound(new { message = "Subscription plan not found." });

            return Ok(updated);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _subscriptionPlanService.DeleteAsync(id);
            if (!deleted)
                return NotFound(new { message = "Subscription plan not found." });

            return Ok(new { message = "Subscription plan deleted successfully." });
        }
    }
}
