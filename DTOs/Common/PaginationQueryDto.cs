namespace onlineStore.DTOs.Common
{
    public class PaginationQueryDto
    {
        private const int DefaultPage = 1;
        private const int DefaultPageSize = 12;
        private const int MaxPageSize = 60;

        public int? Page { get; set; }
        public int? PageSize { get; set; }

        public bool HasPagination => Page.HasValue || PageSize.HasValue;

        public int NormalizedPage => Math.Max(DefaultPage, Page ?? DefaultPage);

        public int NormalizedPageSize => Math.Clamp(
            PageSize ?? DefaultPageSize,
            1,
            MaxPageSize);
    }
}
