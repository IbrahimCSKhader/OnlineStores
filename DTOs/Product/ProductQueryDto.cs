using onlineStore.DTOs.Common;

namespace onlineStore.DTOs.Product
{
    public sealed class ProductQueryDto : PaginationQueryDto
    {
        public string? Search { get; set; }
        public string? Sort { get; set; }
        public bool? OnlyInStock { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

        public bool HasFilters =>
            !string.IsNullOrWhiteSpace(Search) ||
            !string.IsNullOrWhiteSpace(Sort) ||
            OnlyInStock.HasValue ||
            MinPrice.HasValue ||
            MaxPrice.HasValue;

        public bool RequiresPagedResponse => HasPagination || HasFilters;
    }
}
