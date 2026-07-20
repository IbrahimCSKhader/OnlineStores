namespace onlineStore.DTOs.SystemStorage
{
    public class RemoveEmptyProductImagesFoldersResultDto
    {
        public bool DryRun { get; set; } = true;
        public int StoresProcessed { get; set; }
        public int ProductsProcessed { get; set; }
        public int FoldersFound { get; set; }
        public int FoldersDeleted { get; set; }
        public int FoldersWouldBeDeleted { get; set; }
        public int FoldersMissing { get; set; }
        public int FoldersSkippedBecauseReferenced { get; set; }
        public int FoldersSkippedBecauseNotEmpty { get; set; }
        public int FoldersSkippedBecauseError { get; set; }
        public List<ProductImagesFolderCleanupItemDto> WouldDelete { get; set; } = new();
        public List<ProductImagesFolderCleanupItemDto> Deleted { get; set; } = new();
        public List<ProductImagesFolderCleanupItemDto> Skipped { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class ProductImagesFolderCleanupItemDto
    {
        public Guid StoreId { get; set; }
        public Guid ProductId { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
