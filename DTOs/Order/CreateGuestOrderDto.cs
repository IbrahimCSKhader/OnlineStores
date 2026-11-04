using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Order;

public class CreateGuestOrderDto : CreateOrderDto
{
    [MinLength(1), MaxLength(100)]
    public List<GuestOrderItemDto> Items { get; set; } = new();
}

public class GuestOrderItemDto
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    [Range(1, 10000)]
    public int Quantity { get; set; }
    public Guid? BundleOfferId { get; set; }
    public Guid? BundleGroupId { get; set; }
    [Range(1, 10000)]
    public int? BundleQuantity { get; set; }
    [Range(1, 10000)]
    public int? BundleItemQuantity { get; set; }
}
