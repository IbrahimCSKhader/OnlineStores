using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace onlineStore.Models.Offers
{
    [Index(nameof(OfferId), nameof(ProductId), IsUnique = true)]
    [Index(nameof(ProductId))]
    public class OfferItem : BaseEntity
    {
        public Guid OfferId { get; set; }
        public Offer Offer { get; set; } = null!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        [Range(1, int.MaxValue)]
        public int RequiredQuantity { get; set; } = 1;
    }
}
