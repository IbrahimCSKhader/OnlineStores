using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Store;
using onlineStore.Models;

namespace onlineStore.Services.Store;

// Stage changes in the caller's unit of work, after its store authorization check.
public static class CatalogTranslations
{
    public static async Task StageAsync(AppDbContext db, Guid storeId, string entityType,
        Guid entityId, List<CatalogTranslationDto>? input, CancellationToken ct = default)
    {
        if (input == null || input.Count == 0) return;
        var store = await db.Stores.AsNoTracking().SingleAsync(s => s.Id == storeId, ct);
        if (!store.EnableLanguageSwitching)
            throw new InvalidOperationException("فعّل تبديل اللغة قبل إضافة ترجمات.");
        if (input.Count > 2 || input.Any(t => !StoreLanguages.Supported.Contains(t.Language) ||
            t.Language == StoreLanguages.NormalizeOrDefault(store.Language)) ||
            input.Select(t => t.Language).Distinct().Count() != input.Count)
            throw new InvalidOperationException("اختر لغة إضافية صحيحة دون تكرار.");
        var fields = entityType == StoreTranslationEntityTypes.Product
            ? new[] { "Name", "Description", "ShortDescription" } : new[] { "Name", "Description" };
        foreach (var t in input)
        {
            if ((t.Name?.Length ?? 0) > (entityType == StoreTranslationEntityTypes.Product ? 200 : 100) ||
                (t.Description?.Length ?? 0) > (entityType == StoreTranslationEntityTypes.Product ? 2000 : 500) ||
                (t.ShortDescription?.Length ?? 0) > 500 ||
                (entityType != StoreTranslationEntityTypes.Product && t.ShortDescription != null))
                throw new InvalidOperationException("حقول الترجمة تتجاوز الحدود المسموحة.");
        }
        var rows = await db.StoreTranslations.IgnoreQueryFilters().Where(t => t.StoreId == storeId &&
            t.EntityType == entityType && t.EntityId == entityId).ToListAsync(ct);
        foreach (var t in input)
        foreach (var field in fields)
        {
            var value = (field == "Name" ? t.Name : field == "Description" ? t.Description : t.ShortDescription)?.Trim();
            var row = rows.SingleOrDefault(r => r.Language == t.Language && r.FieldName == field);
            if (string.IsNullOrEmpty(value))
            {
                if (row != null) db.StoreTranslations.Remove(row);
                continue;
            }
            if (row == null)
            {
                row = new StoreTranslation { StoreId = storeId, EntityType = entityType,
                    EntityId = entityId, Language = t.Language, FieldName = field };
                db.StoreTranslations.Add(row);
            }
            row.Value = value;
            row.IsDeleted = false;
        }
    }

    public static async Task<Dictionary<Guid, List<CatalogTranslationDto>>> ReadAsync(AppDbContext db,
        Guid storeId, string entityType, IEnumerable<Guid> entityIds, CancellationToken ct = default)
    {
        var ids = entityIds.Distinct().ToList();
        var rows = await db.StoreTranslations.AsNoTracking().Where(t => t.StoreId == storeId &&
            t.EntityType == entityType && ids.Contains(t.EntityId)).ToListAsync(ct);
        return rows.GroupBy(t => t.EntityId).ToDictionary(g => g.Key, g => g.GroupBy(t => t.Language)
            .Select(l => new CatalogTranslationDto { Language = l.Key,
                Name = l.FirstOrDefault(t => t.FieldName == "Name")?.Value,
                Description = l.FirstOrDefault(t => t.FieldName == "Description")?.Value,
                ShortDescription = l.FirstOrDefault(t => t.FieldName == "ShortDescription")?.Value }).ToList());
    }
}
