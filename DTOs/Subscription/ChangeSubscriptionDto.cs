using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Subscription
{
    public class ChangeSubscriptionDto
    {
        [Required]
        public Guid StoreId { get; set; }

        [Required]
        public Guid NewPlanId { get; set; }
    }
}
