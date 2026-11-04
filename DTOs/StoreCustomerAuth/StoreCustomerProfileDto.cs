using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.StoreCustomerAuth;

public class UpdateStoreCustomerProfileDto
{
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    [MaxLength(50), Phone]
    public string? Phone { get; set; }
    [MaxLength(300)]
    public string? Address { get; set; }
    [MaxLength(100)]
    public string? City { get; set; }
}
