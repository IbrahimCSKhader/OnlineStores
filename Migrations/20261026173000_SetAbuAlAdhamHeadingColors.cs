using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using onlineStore.Data;

namespace onlineStore.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261026173000_SetAbuAlAdhamHeadingColors")]
public class SetAbuAlAdhamHeadingColors : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // A requested branding change for this store only. Preserve all other overrides.
        migrationBuilder.Sql("""
            INSERT INTO [StoreAppearance] ([StoreId], [OverridesJson])
            SELECT [Id], N'{"Light":{},"Dark":{}}'
            FROM [Stores] AS s
            WHERE s.[Slug] = N'abu-al-adham-sweets' AND s.[IsDeleted] = 0
              AND NOT EXISTS (SELECT 1 FROM [StoreAppearance] AS a WHERE a.[StoreId] = s.[Id]);

            UPDATE a
            SET [OverridesJson] = JSON_MODIFY(
                JSON_MODIFY(
                    JSON_MODIFY(
                        JSON_MODIFY(
                            JSON_MODIFY(
                                JSON_MODIFY(a.[OverridesJson], '$.Light',
                                    JSON_QUERY(COALESCE(JSON_QUERY(a.[OverridesJson], '$.Light'), N'{}'))),
                                '$.Dark', JSON_QUERY(COALESCE(JSON_QUERY(a.[OverridesJson], '$.Dark'), N'{}'))),
                            '$.Light.storeTitleColor', N'#8B0000'),
                        '$.Light.storeDescriptionColor', N'#8B0000'),
                    '$.Dark.storeTitleColor', N'#8B0000'),
                '$.Dark.storeDescriptionColor', N'#8B0000')
            FROM [StoreAppearance] AS a
            INNER JOIN [Stores] AS s ON s.[Id] = a.[StoreId]
            WHERE s.[Slug] = N'abu-al-adham-sweets' AND s.[IsDeleted] = 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Previous colors are unknown. Keep branding on rollback rather than erase
        // settings the administrator may have subsequently edited.
    }
}
