using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Offer
{
    public class CreateOfferItemDto
    {
        [Required]
        public Guid ProductId { get; set; }

        [Range(1, int.MaxValue)]
        public int RequiredQuantity { get; set; } = 1;
    }
}
