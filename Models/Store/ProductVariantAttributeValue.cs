using Microsoft.EntityFrameworkCore;

namespace onlineStore.Models
{
    [Index(nameof(VariantId))]
    [Index(nameof(AttributeValueId))]
    [Index(nameof(VariantId), nameof(AttributeValueId), IsUnique = true)]
    public class ProductVariantAttributeValue : BaseEntity
    {
        public Guid VariantId { get; set; }
        public ProductVariant Variant { get; set; } = default!;

        public Guid AttributeValueId { get; set; }
        public ProductAttributeValue AttributeValue { get; set; } = default!;
    }
}
