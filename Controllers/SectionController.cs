// Controllers/SectionController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Section;
using onlineStore.Services.Section;

[ApiController]
[Route("api/[controller]")]
public class SectionController : ControllerBase
{
    private readonly ISectionService _sectionService;

    public SectionController(ISectionService sectionService)
    {
        _sectionService = sectionService;
    }

    [HttpGet("store/{storeId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByStore(Guid storeId)
    {
        var sections = await _sectionService
            .GetStoreSectionsAsync(storeId);
        return Ok(sections);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id)
    {
        var section = await _sectionService.GetSectionByIdAsync(id);
        if (section == null)
            return NotFound(new { message = "this section does not exist" });
        return Ok(section);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,StoreOwner")]
    public async Task<IActionResult> Create([FromBody] CreateSectionDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        var section = await _sectionService.CreateSectionAsync(dto);
        return Ok(section);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "SuperAdmin,StoreOwner")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateSectionDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        var section = await _sectionService.UpdateSectionAsync(id, dto);
        if (section == null)
            return NotFound(new { message = "section not found" });
        return Ok(section);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin,StoreOwner")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _sectionService.DeleteSectionAsync(id);
        if (!result)
            return NotFound(new { message = "section does not exist" });
        return Ok(new { message = "section deleted done" });
    }
}