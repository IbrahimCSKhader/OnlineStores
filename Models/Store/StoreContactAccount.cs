using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace onlineStore.Models
{
    [Index(nameof(StoreId), nameof(SortOrder))]
    public class StoreContactAccount : BaseEntity
    {
        public Guid StoreId { get; set; }

        [Required, MaxLength(20)]
        public string Platform { get; set; } = null!;

        [Required, MaxLength(100)]
        public string Username { get; set; } = null!;

        [MaxLength(100)]
        public string? Label { get; set; }

        public int SortOrder { get; set; }

        public Store Store { get; set; } = null!;
    }
}
