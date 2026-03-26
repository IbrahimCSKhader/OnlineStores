// DTOs/Product/ProductAttributeValueDto.cs
using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Product
{
    public class ProductAttributeValueDto
    {
        public Guid Id { get; set; }
        public string AttributeName { get; set; }
        public string Value { get; set; }
    }

    public class CreateProductAttributeValueDto
    {
        public Guid AttributeId { get; set; }
        [Required, MaxLength(200)]
        public string Value { get; set; }
    }
}