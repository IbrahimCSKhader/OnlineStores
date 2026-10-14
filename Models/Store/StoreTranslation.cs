using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace onlineStore.Models;

[Index(
    nameof(StoreId),
    nameof(EntityType),
    nameof(EntityId),
    nameof(Language),
    nameof(FieldName),
    IsUnique = true)]
public class StoreTranslation : BaseEntity
{
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = null!;

    [Required, MaxLength(40)]
    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    [Required, MaxLength(1)]
    public string Language { get; set; } = StoreLanguages.Arabic;

    [Required, MaxLength(80)]
    public string FieldName { get; set; } = string.Empty;

    [Required, MaxLength(100000)]
    public string Value { get; set; } = string.Empty;
}
