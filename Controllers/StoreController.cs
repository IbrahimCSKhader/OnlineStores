// Controllers/StoreController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Store;
using onlineStore.Services.Store;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StoreController : ControllerBase
    {
        private readonly IStoreService _storeService;

        public StoreController(IStoreService storeService)
        {
            _storeService = storeService;
        }



        [HttpGet]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetAll()
        {
            var stores = await _storeService.GetAllStoresAsync();
            return Ok(stores);
        }



        [HttpGet("{id}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var store = await _storeService.GetStoreByIdAsync(id);

            if (store == null)
                return NotFound(new { message = "sorry the store doesnt exist" });

            return Ok(store);
        }



        [HttpGet("slug/{slug}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var store = await _storeService.GetStoreBySlugAsync(slug);

            if (store == null)
                return NotFound(new { message = "المتجر غير موجود" });

            return Ok(store);
        }


         [HttpPost]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateStoreDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var store = await _storeService.CreateStoreAsync(dto, userId!);
            return Ok(store);
        }


        // ════════════════════════════════════════════════════
        // PUT api/store/{id} — SuperAdmin و StoreOwner
        // ════════════════════════════════════════════════════
        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> Update(
            Guid id, [FromBody] UpdateStoreDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var store = await _storeService.UpdateStoreAsync(id, dto);

            if (store == null)
                return NotFound(new { message = "المتجر غير موجود" });

            return Ok(store);
        }


        
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _storeService.DeleteStoreAsync(id);

            if (!result)
                return NotFound(new { message = "the store does not exist" });

            return Ok(new { message = "soft delete done " });
        }
    }
}
