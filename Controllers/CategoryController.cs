// Controllers/CategoryController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Category;
using onlineStore.Services.Category;

[ApiController]
[Route("api/[controller]")]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoryController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet("store/{storeId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByStore(Guid storeId)
    {
        var categories = await _categoryService
            .GetStoreCategoriesAsync(storeId);
        return Ok(categories);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id)
    {
        var category = await _categoryService.GetCategoryByIdAsync(id);
        if (category == null)
            return NotFound(new { message = "this category does not exist" });
        return Ok(category);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,StoreOwner")]
    public async Task<IActionResult> Create([FromBody] CreateCategoryDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        var category = await _categoryService.CreateCategoryAsync(dto);
        return Ok(category);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "SuperAdmin,StoreOwner")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateCategoryDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        var category = await _categoryService.UpdateCategoryAsync(id, dto);
        if (category == null)
            return NotFound(new { message = "this category does not exist" });
        return Ok(category);
    }

    // DELETE api/category/{id}
    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin,StoreOwner")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _categoryService.DeleteCategoryAsync(id);
        if (!result)
            return NotFound(new { message = "this category does not exist" });
        return Ok(new { message = "(soft) delete done" });
    }
}