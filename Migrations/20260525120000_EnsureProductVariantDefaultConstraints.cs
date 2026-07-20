using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using onlineStore.Data;

#nullable disable

namespace onlineStore.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260525120000_EnsureProductVariantDefaultConstraints")]
    public partial class EnsureProductVariantDefaultConstraints : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID('dbo.ProductVariants', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.ProductVariants', 'IsActive') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
            WHERE dc.parent_object_id = OBJECT_ID('dbo.ProductVariants')
              AND c.name = 'IsActive'
       )
    BEGIN
        ALTER TABLE [ProductVariants]
        ADD CONSTRAINT [DF_ProductVariants_IsActive] DEFAULT CAST(1 AS bit) FOR [IsActive];
    END

    IF COL_LENGTH('dbo.ProductVariants', 'IsDefault') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
            WHERE dc.parent_object_id = OBJECT_ID('dbo.ProductVariants')
              AND c.name = 'IsDefault'
       )
    BEGIN
        ALTER TABLE [ProductVariants]
        ADD CONSTRAINT [DF_ProductVariants_IsDefault] DEFAULT CAST(0 AS bit) FOR [IsDefault];
    END

    IF COL_LENGTH('dbo.ProductVariants', 'SortOrder') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
            WHERE dc.parent_object_id = OBJECT_ID('dbo.ProductVariants')
              AND c.name = 'SortOrder'
       )
    BEGIN
        ALTER TABLE [ProductVariants]
        ADD CONSTRAINT [DF_ProductVariants_SortOrder] DEFAULT 0 FOR [SortOrder];
    END
END
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID('dbo.ProductVariants', 'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE [name] = 'DF_ProductVariants_IsActive')
        ALTER TABLE [ProductVariants] DROP CONSTRAINT [DF_ProductVariants_IsActive];

    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE [name] = 'DF_ProductVariants_IsDefault')
        ALTER TABLE [ProductVariants] DROP CONSTRAINT [DF_ProductVariants_IsDefault];

    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE [name] = 'DF_ProductVariants_SortOrder')
        ALTER TABLE [ProductVariants] DROP CONSTRAINT [DF_ProductVariants_SortOrder];
END
""");
        }
    }
}
