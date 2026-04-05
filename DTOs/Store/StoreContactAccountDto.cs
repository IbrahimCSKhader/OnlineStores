using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Store
{
    public class StoreContactAccountDto
    {
        public Guid Id { get; set; }
        public string Platform { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string? Label { get; set; }
        public string Url { get; set; } = null!;
        public int SortOrder { get; set; }
    }

    public class StoreContactAccountInputDto
    {
        [Required, MaxLength(20)]
        public string Platform { get; set; } = null!;

        [Required, MaxLength(100)]
        public string Username { get; set; } = null!;

        [MaxLength(100)]
        public string? Label { get; set; }

        public int SortOrder { get; set; }
    }
}
