using System.ComponentModel.DataAnnotations;
namespace onlineStore.DTOs.Cart;
public class AddBundleToCartDto
{
    public Guid StoreId { get; set; }
    public Guid OfferId { get; set; }
    [Range(1, 10000)] public int Quantity { get; set; } = 1;
    // Optional variant selection per component; default active variants are used otherwise.
    public Dictionary<Guid, Guid> Variants { get; set; } = new();
}
