namespace onlineStore.DTOs.Common
{
    public sealed class PagedResultDto<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;

        public static PagedResultDto<T> Create(
            IReadOnlyList<T> items,
            int page,
            int pageSize,
            int totalCount)
        {
            var normalizedPageSize = Math.Max(1, pageSize);

            return new PagedResultDto<T>
            {
                Items = items,
                Page = Math.Max(1, page),
                PageSize = normalizedPageSize,
                TotalCount = Math.Max(0, totalCount),
                TotalPages = (int)Math.Ceiling(Math.Max(0, totalCount) / (double)normalizedPageSize)
            };
        }
    }
}
