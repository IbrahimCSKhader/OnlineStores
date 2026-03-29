using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Product;
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

        // GET api/product/store/{storeId}
        [HttpGet("store/{storeId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByStore(Guid storeId)
        {
            var products = await _productService.GetStoreProductsAsync(storeId);
            return Ok(products);
        }

        // GET api/product/featured/{storeId}
        [HttpGet("featured/{storeId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFeatured(Guid storeId)
        {
            var products = await _productService.GetFeaturedProductsAsync(storeId);
            return Ok(products);
        }

        // GET api/product/category/{categoryId}
        [HttpGet("category/{categoryId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByCategory(Guid categoryId)
        {
            var products = await _productService.GetProductsByCategoryAsync(categoryId);
            return Ok(products);
        }

        // GET api/product/section/{sectionId}
        [HttpGet("section/{sectionId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetBySection(Guid sectionId)
        {
            var products = await _productService.GetProductsBySectionAsync(sectionId);
            return Ok(products);
        }

        // GET api/product/{id}
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(Guid id)
        {
            var product = await _productService.GetProductByIdAsync(id);

            if (product == null)
                return NotFound(new { message = "المنتج غير موجود" });

            return Ok(product);
        }

        // GET api/product/slug/{slug}
        [HttpGet("slug/{slug}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var product = await _productService.GetProductBySlugAsync(slug);

            if (product == null)
                return NotFound(new { message = "المنتج غير موجود" });

            return Ok(product);
        }

        // POST api/product
        [HttpPost]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> Create([FromForm] CreateProductDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var product = await _productService.CreateProductAsync(dto);
            return Ok(product);
        }

        // PUT api/product/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var product = await _productService.UpdateProductAsync(id, dto);

            if (product == null)
                return NotFound(new { message = "المنتج غير موجود" });

            return Ok(product);
        }

        // DELETE api/product/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _productService.DeleteProductAsync(id);

            if (!result)
                return NotFound(new { message = "المنتج غير موجود" });

            return Ok(new { message = "تم حذف المنتج بنجاح" });
        }

        // POST api/product/image
        [HttpPost("image")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> AddImage([FromForm] AddProductImageDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var image = await _productService.AddImageAsync(dto);
            return Ok(image);
        }

        // DELETE api/product/image/{imageId}
        [HttpDelete("image/{imageId}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> DeleteImage(Guid imageId)
        {
            var result = await _productService.DeleteImageAsync(imageId);

            if (!result)
                return NotFound(new { message = "الصورة غير موجودة" });

            return Ok(new { message = "تم حذف الصورة بنجاح" });
        }

        // POST api/product/{productId}/variant
        [HttpPost("{productId}/variant")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> AddVariant(
            Guid productId,
            [FromBody] CreateProductVariantDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var variant = await _productService.AddVariantAsync(productId, dto);
            return Ok(variant);
        }

        // DELETE api/product/variant/{variantId}
        [HttpDelete("variant/{variantId}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> DeleteVariant(Guid variantId)
        {
            var result = await _productService.DeleteVariantAsync(variantId);

            if (!result)
                return NotFound(new { message = "النسخة غير موجودة" });

            return Ok(new { message = "تم حذف النسخة بنجاح" });
        }

        // POST api/product/{id}/visit
        [HttpPost("{id}/visit")]
        [AllowAnonymous]
        public async Task<IActionResult> IncrementVisit(Guid id)
        {
            var visitCount = await _productService.IncrementProductVisitAsync(id);

            if (visitCount == null)
                return NotFound(new { message = "product does not exist" });

            return Ok(new
            {
                message = "visit incremented successfully",
                productId = id,
                visitCount = visitCount.Value
            });
        }

        // GET api/product/{id}/visit-count
        [HttpGet("{id}/visit-count")]
        [AllowAnonymous]
        public async Task<IActionResult> GetVisitCount(Guid id)
        {
            var visitCount = await _productService.GetProductVisitCountAsync(id);

            if (visitCount == null)
                return NotFound(new { message = "product does not exist" });

            return Ok(new
            {
                productId = id,
                visitCount = visitCount.Value
            });
        }
    }
}