namespace onlineStore.DTOs.SystemStorage
{
    public class EnsureVariantFoldersResultDto
    {
        public int StoresProcessed { get; set; }
        public int ProductsProcessed { get; set; }
        public int VariantsProcessed { get; set; }
        public int FoldersCreated { get; set; }
        public int FoldersAlreadyExisted { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
