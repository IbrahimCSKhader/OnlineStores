using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    public partial class StabilizeProductVariantPricingAndSku : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH('dbo.ProductVariants', 'PriceOverride') IS NOT NULL
   AND COL_LENGTH('dbo.ProductVariants', 'Price') IS NOT NULL
BEGIN
    UPDATE [ProductVariants]
    SET [Price] = [PriceOverride]
    WHERE [Price] IS NULL
        AND [PriceOverride] IS NOT NULL;
END
""");

            migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ProductVariants_SKU' AND [object_id] = OBJECT_ID('dbo.ProductVariants'))
    DROP INDEX [IX_ProductVariants_SKU] ON [ProductVariants];

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ProductVariants_ProductId_SKU' AND [object_id] = OBJECT_ID('dbo.ProductVariants'))
    CREATE UNIQUE INDEX [IX_ProductVariants_ProductId_SKU]
        ON [ProductVariants] ([ProductId], [SKU])
        WHERE [SKU] IS NOT NULL;
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('dbo.ProductVariants', 'PriceOverride') IS NOT NULL
    ALTER TABLE [ProductVariants] DROP COLUMN [PriceOverride];
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH('dbo.ProductVariants', 'PriceOverride') IS NULL
    ALTER TABLE [ProductVariants] ADD [PriceOverride] decimal(18,2) NULL;

IF COL_LENGTH('dbo.ProductVariants', 'PriceOverride') IS NOT NULL
   AND COL_LENGTH('dbo.ProductVariants', 'Price') IS NOT NULL
BEGIN
    UPDATE [ProductVariants]
    SET [PriceOverride] = [Price]
    WHERE [PriceOverride] IS NULL
        AND [Price] IS NOT NULL;
END
""");

            migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ProductVariants_ProductId_SKU' AND [object_id] = OBJECT_ID('dbo.ProductVariants'))
    DROP INDEX [IX_ProductVariants_ProductId_SKU] ON [ProductVariants];

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ProductVariants_SKU' AND [object_id] = OBJECT_ID('dbo.ProductVariants'))
    CREATE UNIQUE INDEX [IX_ProductVariants_SKU]
        ON [ProductVariants] ([SKU])
        WHERE [SKU] IS NOT NULL;
""");
        }
    }
}
