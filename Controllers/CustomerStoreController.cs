using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.CustomerStore;
using onlineStore.Services.CustomerStore;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin,StoreOwner")]
    public class CustomerStoreController : ControllerBase
    {
        private readonly ICustomerStoreService _customerStoreService;

        public CustomerStoreController(ICustomerStoreService customerStoreService)
        {
            _customerStoreService = customerStoreService;
        }

        [HttpGet("customers")]
        public async Task<IActionResult> GetAllCustomers()
        {
            try
            {
                var data = await _customerStoreService.GetAllCustomersAsync();
                return Ok(data);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
        }

        [HttpGet("store/{storeId}")]
        public async Task<IActionResult> GetStoreCustomers(Guid storeId)
        {
            try
            {
                var data = await _customerStoreService.GetStoreCustomersAsync(storeId);
                return Ok(data);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCustomerStoreDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var result = await _customerStoreService.CreateAsync(dto);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerStoreDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var result = await _customerStoreService.UpdateAsync(id, dto);

                if (result == null)
                    return NotFound(new { message = "record Does not exist" });

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var result = await _customerStoreService.DeleteAsync(id);

                if (!result)
                    return NotFound(new { message = "record Does not exist" });

                return Ok(new { message = "delete done" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
        }
    }
}
