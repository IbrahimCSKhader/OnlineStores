using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using onlineStore.DTOs.Product;
using onlineStore.Security;
using onlineStore.Services.Product;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }

        private static string GetDatabaseErrorMessage(Exception ex)
        {
            return ex.GetBaseException()?.Message ?? ex.Message;
        }

        // GET api/product/store/{storeId}
        [HttpGet("store/{storeId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByStore(Guid storeId, [FromQuery] ProductQueryDto query)
        {
            try
            {
                var userId = GetUserIdOrNull();
                if (query.RequiresPagedResponse)
                {
                    var pagedProducts = await _productService.GetStoreProductsPageAsync(storeId, query, userId);
                    return Ok(pagedProducts);
                }

                var products = await _productService.GetStoreProductsAsync(storeId, userId);
                return Ok(products);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        [HttpGet("store/{storeId}/manage")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> GetByStoreForManagement(Guid storeId)
        {
            try
            {
                var products = await _productService.GetStoreProductsForManagementAsync(storeId);
                return Ok(products);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        // GET api/product/featured/{storeId}
        [HttpGet("featured/{storeId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFeatured(Guid storeId)
        {
            try
            {
                var userId = GetUserIdOrNull();
                var products = await _productService.GetFeaturedProductsAsync(storeId, userId);
                return Ok(products);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        // GET api/product/category/{categoryId}
        [HttpGet("category/{categoryId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByCategory(Guid categoryId, [FromQuery] ProductQueryDto query)
        {
            try
            {
                var userId = GetUserIdOrNull();
                if (query.RequiresPagedResponse)
                {
                    var pagedProducts = await _productService.GetProductsByCategoryPageAsync(categoryId, query, userId);
                    return Ok(pagedProducts);
                }

                var products = await _productService.GetProductsByCategoryAsync(categoryId, userId);
                return Ok(products);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        // GET api/product/section/{sectionId}
        [HttpGet("section/{sectionId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetBySection(Guid sectionId, [FromQuery] ProductQueryDto query)
        {
            try
            {
                var userId = GetUserIdOrNull();
                if (query.RequiresPagedResponse)
                {
                    var pagedProducts = await _productService.GetProductsBySectionPageAsync(sectionId, query, userId);
                    return Ok(pagedProducts);
                }

                var products = await _productService.GetProductsBySectionAsync(sectionId, userId);
                return Ok(products);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        // GET api/product/{id}
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var userId = GetUserIdOrNull();
                var product = await _productService.GetProductByIdAsync(id, userId);

                if (product == null)
                    return NotFound(new { message = "المنتج غير موجود" });

                return Ok(product);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        // GET api/product/slug/{slug}
        [HttpGet("slug/{slug}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            try
            {
                var userId = GetUserIdOrNull();
                var product = await _productService.GetProductBySlugAsync(slug, userId);

                if (product == null)
                    return NotFound(new { message = "المنتج غير موجود" });

                return Ok(product);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        // POST api/product
        [HttpPost]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] CreateProductDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var product = await _productService.CreateProductAsync(dto);
                return Ok(product);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(new { message = GetDatabaseErrorMessage(ex) });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // PUT api/product/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Update(Guid id, [FromForm] UpdateProductDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var product = await _productService.UpdateProductAsync(id, dto);

                if (product == null)
                    return NotFound(new { message = "المنتج غير موجود" });

                return Ok(product);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(new { message = GetDatabaseErrorMessage(ex) });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // DELETE api/product/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var result = await _productService.DeleteProductAsync(id);

                if (!result)
                    return NotFound(new { message = "المنتج غير موجود" });

                return Ok(new { message = "تم حذف المنتج بنجاح" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(new { message = GetDatabaseErrorMessage(ex) });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST api/product/image
        [HttpPost("image")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> AddImage([FromForm] AddProductImageDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var image = await _productService.AddImageAsync(dto);
                return Ok(image);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(new { message = GetDatabaseErrorMessage(ex) });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // DELETE api/product/image/{imageId}
        [HttpDelete("image/{imageId}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> DeleteImage(Guid imageId)
        {
            try
            {
                var result = await _productService.DeleteImageAsync(imageId);

                if (!result)
                    return NotFound(new { message = "الصورة غير موجودة" });

                return Ok(new { message = "تم حذف الصورة بنجاح" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST api/product/{productId}/variant
        [HttpPost("{productId}/variant")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> AddVariant(Guid productId, [FromBody] CreateProductVariantDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var variant = await _productService.AddVariantAsync(productId, dto);
                return Ok(variant);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(new { message = GetDatabaseErrorMessage(ex) });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // PUT api/product/variant/{variantId}
        [HttpPut("variant/{variantId}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> UpdateVariant(Guid variantId, [FromBody] UpdateProductVariantDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var variant = await _productService.UpdateVariantAsync(variantId, dto);

                if (variant == null)
                    return NotFound(new { message = "النسخة غير موجودة" });

                return Ok(variant);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(new { message = GetDatabaseErrorMessage(ex) });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // DELETE api/product/variant/{variantId}
        [HttpDelete("variant/{variantId}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> DeleteVariant(Guid variantId)
        {
            try
            {
                var result = await _productService.DeleteVariantAsync(variantId);

                if (!result)
                    return NotFound(new { message = "النسخة غير موجودة" });

                return Ok(new { message = "تم حذف النسخة بنجاح" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST api/product/{productId}/visit
        [HttpPost("{productId}/visit")]
        [AllowAnonymous]
        public async Task<IActionResult> IncrementVisit(Guid productId)
        {
            var count = await _productService.IncrementProductVisitAsync(productId);

            if (count == null)
                return NotFound(new { message = "المنتج غير موجود" });

            return Ok(new { visitCount = count });
        }

        // GET api/product/{productId}/visit-count
        [HttpGet("{productId}/visit-count")]
        [AllowAnonymous]
        public async Task<IActionResult> GetVisitCount(Guid productId)
        {
            try
            {
                var count = await _productService.GetProductVisitCountAsync(productId);

                if (count == null)
                    return NotFound(new { message = "المنتج غير موجود" });

                return Ok(new { visitCount = count });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
        }

        private Guid? GetUserIdOrNull()
        {
            return User.GetStoreCustomerId();
        }
    }
}
