using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Store;

public class StoreDeliveryLocationDto
{
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "99999999.99")]
    public decimal Cost { get; set; }
}
