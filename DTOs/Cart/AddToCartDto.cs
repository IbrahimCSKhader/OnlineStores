using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Cart
{
    public class AddToCartDto
    {
        [Required]
        public Guid ProductId { get; set; }

        public Guid? VariantId { get; set; }

        [Required]
        [Range(1, 100)]
        public int Quantity { get; set; } = 1;

        [Required]
        public Guid StoreId { get; set; }
    }
}
