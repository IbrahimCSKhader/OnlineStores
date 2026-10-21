using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace onlineStore.Models;

public class StoreDeliveryLocation : BaseEntity
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Cost { get; set; }

    public Guid StoreId { get; set; }
    public Store Store { get; set; } = null!;
}
