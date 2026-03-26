using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Review
{
    public class CreateReviewDto
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        public Guid StoreId { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }
    }
}