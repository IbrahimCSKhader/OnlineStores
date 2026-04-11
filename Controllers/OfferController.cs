using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Offer;
using onlineStore.Services.Offer;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/offer")]
    public class OfferController : ControllerBase
    {
        private readonly IOfferService _offerService;

        public OfferController(IOfferService offerService)
        {
            _offerService = offerService;
        }

        [HttpGet("store/{storeId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetStoreOffers(Guid storeId)
        {
            var offers = await _offerService.GetStoreOffersAsync(storeId);
            return Ok(offers);
        }

        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(Guid id)
        {
            var offer = await _offerService.GetOfferByIdAsync(id);
            if (offer == null)
                return NotFound(new { message = "Offer not found." });

            return Ok(offer);
        }

        [HttpPost]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> Create([FromBody] CreateOfferDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var created = await _offerService.CreateOfferAsync(dto);
            return Ok(created);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOfferDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var updated = await _offerService.UpdateOfferAsync(id, dto);
            if (updated == null)
                return NotFound(new { message = "Offer not found." });

            return Ok(updated);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _offerService.DeleteOfferAsync(id);
            if (!deleted)
                return NotFound(new { message = "Offer not found." });

            return Ok(new { message = "Offer deleted successfully." });
        }
    }
}
