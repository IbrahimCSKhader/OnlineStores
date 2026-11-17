using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using onlineStore.Data;

namespace onlineStore.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261117020000_UseJubehLogoAsStoreCover")]
public partial class UseJubehLogoAsStoreCover : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [Stores]
            SET [CoverImageUrl] = [LogoUrl]
            WHERE [Slug] = N'jubeh-office-furniture'
              AND NULLIF([LogoUrl], N'') IS NOT NULL
              AND (NULLIF([CoverImageUrl], N'') IS NULL
                   OR [CoverImageUrl] = N'/wwwroot/store-branding/jubeh/ribbed-desk.jpg');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [Stores]
            SET [CoverImageUrl] = N'/wwwroot/store-branding/jubeh/ribbed-desk.jpg'
            WHERE [Slug] = N'jubeh-office-furniture' AND [CoverImageUrl] = [LogoUrl];
            """);
    }
}
