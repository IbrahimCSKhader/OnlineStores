using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Subscription
{
    public class AssignSubscriptionDto
    {
        [Required]
        public Guid StoreId { get; set; }

        [Required]
        public Guid PlanId { get; set; }
    }
}
