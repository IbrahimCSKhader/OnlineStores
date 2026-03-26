using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Review
{
    public class UpdateReviewApprovalDto
    {
        [Required]
        public bool IsApproved { get; set; }
    }
}