using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Category;
using onlineStore.Services.Category;

[ApiController]
[Route("api/[controller]")]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;
    private readonly ILogger<CategoryController> _logger;

    public CategoryController(
        ICategoryService categoryService,
        ILogger<CategoryController> logger)
    {
        _categoryService = categoryService;
        _logger = logger;
    }

    [HttpGet("store/{storeId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByStore(
        Guid storeId,
        CancellationToken cancellationToken)
    {
        var categories = await _categoryService.GetStoreCategoriesAsync(
            storeId,
            cancellationToken);

        return Ok(categories);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var category = await _categoryService.GetCategoryByIdAsync(
            id,
            cancellationToken);

        if (category == null)
            return NotFound(new { message = "this category does not exist" });

        return Ok(category);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,StoreOwner")]
    public async Task<IActionResult> Create(
        [FromBody] CreateCategoryDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var category = await _categoryService.CreateCategoryAsync(
                dto,
                cancellationToken);

            return Ok(category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Create category failed. StoreId: {StoreId}, Name: {Name}, Slug: {Slug}",
                dto.StoreId, dto.Name, dto.Slug);

            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "SuperAdmin,StoreOwner")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCategoryDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var category = await _categoryService.UpdateCategoryAsync(
                id,
                dto,
                cancellationToken);

            if (category == null)
                return NotFound(new { message = "this category does not exist" });

            return Ok(category);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex,
                "Unauthorized update attempt for category {CategoryId}",
                id);

            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex,
                "Category or store not found while updating category {CategoryId}",
                id);

            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex,
                "Invalid update request for category {CategoryId}",
                id);

            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while updating category {CategoryId}",
                id);

            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin,StoreOwner")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _categoryService.DeleteCategoryAsync(
                id,
                cancellationToken);

            if (!result)
                return NotFound(new { message = "this category does not exist" });

            return Ok(new { message = "soft delete done" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Delete category failed. CategoryId: {CategoryId}",
                id);

            return BadRequest(new { message = ex.Message });
        }
    }
}