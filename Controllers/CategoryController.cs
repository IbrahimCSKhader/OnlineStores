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
        [FromQuery] string? language,
        [FromQuery] string? lang,
        CancellationToken cancellationToken)
    {
        var categories = await _categoryService.GetStoreCategoriesAsync(
            storeId,
            language ?? lang,
            cancellationToken);

        return Ok(categories);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromQuery] string? language,
        [FromQuery] string? lang,
        CancellationToken cancellationToken)
    {
        var category = await _categoryService.GetCategoryByIdAsync(
            id,
            language ?? lang,
            cancellationToken);

        if (category == null)
            return NotFound(new { message = "this category does not exist" });

        return Ok(category);
    }

    [HttpPost]
    [Consumes("application/json")]
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
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex,
                "Unauthorized create attempt for category in store {StoreId}",
                dto.StoreId);

            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Create category failed. StoreId: {StoreId}, Name: {Name}, Slug: {Slug}",
                dto.StoreId, dto.Name, dto.Slug);

            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [Authorize(Roles = "SuperAdmin,StoreOwner")]
    public Task<IActionResult> CreateWithImage(
        [FromForm] CreateCategoryDto dto,
        CancellationToken cancellationToken) => Create(dto, cancellationToken);

    [HttpPut("{id}")]
    [Consumes("application/json")]
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

            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
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

    [HttpPut("{id}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [Authorize(Roles = "SuperAdmin,StoreOwner")]
    public Task<IActionResult> UpdateWithImage(
        Guid id,
        [FromForm] UpdateCategoryDto dto,
        CancellationToken cancellationToken) => Update(id, dto, cancellationToken);

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
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex,
                "Unauthorized delete attempt for category {CategoryId}",
                id);

            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
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
