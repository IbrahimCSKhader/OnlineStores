// DTOs/Product/ProductVisitDto.cs
namespace onlineStore.DTOs.Product
{
    public class ProductVisitDto
    {
        public Guid ProductId { get; set; }
        public int VisitCount { get; set; }
    }
}