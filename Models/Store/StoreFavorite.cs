namespace onlineStore.Models;

public class StoreFavorite
{
    public Guid StoreId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
}
