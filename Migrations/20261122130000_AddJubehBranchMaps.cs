using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using onlineStore.Data;
namespace onlineStore.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20261122130000_AddJubehBranchMaps")]
public class AddJubehBranchMaps : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""

            UPDATE a SET [OverridesJson] = JSON_MODIFY(a.[OverridesJson],
                '$.Sections.Locations[' + b.[key] + '].MapUrl', N'https://www.google.com/maps?q=31.9013979,35.206875&z=17&hl=en')
            FROM [StoreAppearance] a JOIN [Stores] s ON s.[Id] = a.[StoreId]
            CROSS APPLY OPENJSON(a.[OverridesJson], '$.Sections.Locations') b
            WHERE s.[Slug] = N'jubeh-office-furniture'
              AND JSON_VALUE(b.[value], '$.Name') = N'الفرع الأول – رام الله';

            UPDATE a SET [OverridesJson] = JSON_MODIFY(a.[OverridesJson],
                '$.Sections.Locations[' + b.[key] + '].MapUrl', N'https://www.google.com/maps/search/Nidal%20Jubeh%20%D8%A7%D9%84%D8%AC%D8%B9%D8%A8%D8%A9%20%D9%84%D9%84%D8%A7%D8%AB%D8%A7%D8%AB%20%D8%A7%D9%84%D9%85%D9%83%D8%AA%D8%A8%D9%8A/@31.8534,35.2385,17z?hl=en')
            FROM [StoreAppearance] a JOIN [Stores] s ON s.[Id] = a.[StoreId]
            CROSS APPLY OPENJSON(a.[OverridesJson], '$.Sections.Locations') b
            WHERE s.[Slug] = N'jubeh-office-furniture'
              AND JSON_VALUE(b.[value], '$.Name') = N'الفرع الثاني – الرام';
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
