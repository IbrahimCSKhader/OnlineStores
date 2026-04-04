// DTOs/Product/ProductAttributeValueDto.cs
using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Product
{
    public class ProductAttributeValueDto
    {
        public Guid Id { get; set; }
        public string AttributeName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class CreateProductAttributeValueDto
    {
        [Required]
        public Guid AttributeId { get; set; }

        [Required, MaxLength(200)]
        public string Value { get; set; } = string.Empty;
    }
}