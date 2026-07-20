using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    public partial class AddSafeProductVariantStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH('dbo.ProductVariants', 'CompareAtPrice') IS NULL
    ALTER TABLE [ProductVariants] ADD [CompareAtPrice] decimal(18,2) NULL;

IF COL_LENGTH('dbo.ProductVariants', 'IsDefault') IS NULL
    ALTER TABLE [ProductVariants] ADD [IsDefault] bit NOT NULL CONSTRAINT [DF_ProductVariants_IsDefault] DEFAULT CAST(0 AS bit);

IF COL_LENGTH('dbo.ProductVariants', 'Price') IS NULL
    ALTER TABLE [ProductVariants] ADD [Price] decimal(18,2) NULL;

IF COL_LENGTH('dbo.ProductVariants', 'SortOrder') IS NULL
    ALTER TABLE [ProductVariants] ADD [SortOrder] int NOT NULL CONSTRAINT [DF_ProductVariants_SortOrder] DEFAULT 0;

IF COL_LENGTH('dbo.ProductImages', 'VariantId') IS NULL
    ALTER TABLE [ProductImages] ADD [VariantId] uniqueidentifier NULL;
""");

            migrationBuilder.Sql("""
IF OBJECT_ID('dbo.ProductVariantAttributeValues', 'U') IS NULL
BEGIN
    CREATE TABLE [ProductVariantAttributeValues] (
        [Id] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [AttributeValueId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_ProductVariantAttributeValues_IsDeleted] DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_ProductVariantAttributeValues] PRIMARY KEY ([Id])
    );
END
""");

            migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ProductVariants_ProductId_IsActive' AND [object_id] = OBJECT_ID('dbo.ProductVariants'))
    CREATE INDEX [IX_ProductVariants_ProductId_IsActive] ON [ProductVariants] ([ProductId], [IsActive]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ProductVariants_ProductId_IsDefault' AND [object_id] = OBJECT_ID('dbo.ProductVariants'))
    CREATE INDEX [IX_ProductVariants_ProductId_IsDefault] ON [ProductVariants] ([ProductId], [IsDefault]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ProductImages_VariantId' AND [object_id] = OBJECT_ID('dbo.ProductImages'))
    CREATE INDEX [IX_ProductImages_VariantId] ON [ProductImages] ([VariantId]);

IF OBJECT_ID('dbo.ProductVariantAttributeValues', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ProductVariantAttributeValues_AttributeValueId' AND [object_id] = OBJECT_ID('dbo.ProductVariantAttributeValues'))
        CREATE INDEX [IX_ProductVariantAttributeValues_AttributeValueId] ON [ProductVariantAttributeValues] ([AttributeValueId]);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ProductVariantAttributeValues_VariantId' AND [object_id] = OBJECT_ID('dbo.ProductVariantAttributeValues'))
        CREATE INDEX [IX_ProductVariantAttributeValues_VariantId] ON [ProductVariantAttributeValues] ([VariantId]);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ProductVariantAttributeValues_VariantId_AttributeValueId' AND [object_id] = OBJECT_ID('dbo.ProductVariantAttributeValues'))
        CREATE UNIQUE INDEX [IX_ProductVariantAttributeValues_VariantId_AttributeValueId] ON [ProductVariantAttributeValues] ([VariantId], [AttributeValueId]);
END
""");

            migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_ProductVariants_Products_ProductId')
    ALTER TABLE [ProductVariants] DROP CONSTRAINT [FK_ProductVariants_Products_ProductId];

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_ProductVariants_Products_ProductId')
    ALTER TABLE [ProductVariants] ADD CONSTRAINT [FK_ProductVariants_Products_ProductId]
        FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_ProductImages_ProductVariants_VariantId')
    ALTER TABLE [ProductImages] ADD CONSTRAINT [FK_ProductImages_ProductVariants_VariantId]
        FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE NO ACTION;

IF OBJECT_ID('dbo.ProductVariantAttributeValues', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_ProductVariantAttributeValues_ProductAttributeValues_AttributeValueId')
        ALTER TABLE [ProductVariantAttributeValues] ADD CONSTRAINT [FK_ProductVariantAttributeValues_ProductAttributeValues_AttributeValueId]
            FOREIGN KEY ([AttributeValueId]) REFERENCES [ProductAttributeValues] ([Id]) ON DELETE NO ACTION;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_ProductVariantAttributeValues_ProductVariants_VariantId')
        ALTER TABLE [ProductVariantAttributeValues] ADD CONSTRAINT [FK_ProductVariantAttributeValues_ProductVariants_VariantId]
            FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE NO ACTION;
END
""");

            migrationBuilder.Sql("""
INSERT INTO [ProductVariants] (
    [Id],
    [Name],
    [SKU],
    [PriceOverride],
    [Price],
    [CompareAtPrice],
    [StockQuantity],
    [ImageUrl],
    [IsDefault],
    [IsActive],
    [SortOrder],
    [ProductId],
    [CreatedAt],
    [UpdatedAt],
    [IsDeleted]
)
SELECT
    NEWID(),
    N'Default',
    CASE
        WHEN p.[SKU] IS NOT NULL
            AND NOT EXISTS (
                SELECT 1
                FROM [ProductVariants] existingVariant
                WHERE existingVariant.[SKU] = p.[SKU]
                    AND existingVariant.[IsDeleted] = CAST(0 AS bit)
            )
            AND NOT EXISTS (
                SELECT 1
                FROM [Products] duplicateProduct
                WHERE duplicateProduct.[Id] <> p.[Id]
                    AND duplicateProduct.[SKU] = p.[SKU]
                    AND duplicateProduct.[IsDeleted] = CAST(0 AS bit)
            )
        THEN p.[SKU]
        ELSE NULL
    END,
    NULL,
    NULL,
    NULL,
    p.[StockQuantity],
    p.[ThumbnailUrl],
    CAST(1 AS bit),
    CAST(1 AS bit),
    0,
    p.[Id],
    SYSUTCDATETIME(),
    NULL,
    CAST(0 AS bit)
FROM [Products] p
WHERE p.[IsDeleted] = CAST(0 AS bit)
    AND NOT EXISTS (
        SELECT 1
        FROM [ProductVariants] v
        WHERE v.[ProductId] = p.[Id]
            AND v.[IsDeleted] = CAST(0 AS bit)
    );
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_ProductImages_ProductVariants_VariantId')
    ALTER TABLE [ProductImages] DROP CONSTRAINT [FK_ProductImages_ProductVariants_VariantId];

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_ProductVariantAttributeValues_ProductAttributeValues_AttributeValueId')
    ALTER TABLE [ProductVariantAttributeValues] DROP CONSTRAINT [FK_ProductVariantAttributeValues_ProductAttributeValues_AttributeValueId];

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_ProductVariantAttributeValues_ProductVariants_VariantId')
    ALTER TABLE [ProductVariantAttributeValues] DROP CONSTRAINT [FK_ProductVariantAttributeValues_ProductVariants_VariantId];

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_ProductVariants_Products_ProductId')
    ALTER TABLE [ProductVariants] DROP CONSTRAINT [FK_ProductVariants_Products_ProductId];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ProductVariantAttributeValues_VariantId_AttributeValueId' AND [object_id] = OBJECT_ID('dbo.ProductVariantAttributeValues'))
    DROP INDEX [IX_ProductVariantAttributeValues_VariantId_AttributeValueId] ON [ProductVariantAttributeValues];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ProductVariantAttributeValues_VariantId' AND [object_id] = OBJECT_ID('dbo.ProductVariantAttributeValues'))
    DROP INDEX [IX_ProductVariantAttributeValues_VariantId] ON [ProductVariantAttributeValues];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ProductVariantAttributeValues_AttributeValueId' AND [object_id] = OBJECT_ID('dbo.ProductVariantAttributeValues'))
    DROP INDEX [IX_ProductVariantAttributeValues_AttributeValueId] ON [ProductVariantAttributeValues];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ProductImages_VariantId' AND [object_id] = OBJECT_ID('dbo.ProductImages'))
    DROP INDEX [IX_ProductImages_VariantId] ON [ProductImages];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ProductVariants_ProductId_IsDefault' AND [object_id] = OBJECT_ID('dbo.ProductVariants'))
    DROP INDEX [IX_ProductVariants_ProductId_IsDefault] ON [ProductVariants];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ProductVariants_ProductId_IsActive' AND [object_id] = OBJECT_ID('dbo.ProductVariants'))
    DROP INDEX [IX_ProductVariants_ProductId_IsActive] ON [ProductVariants];

IF OBJECT_ID('dbo.ProductVariantAttributeValues', 'U') IS NOT NULL
    DROP TABLE [ProductVariantAttributeValues];

IF COL_LENGTH('dbo.ProductImages', 'VariantId') IS NOT NULL
    ALTER TABLE [ProductImages] DROP COLUMN [VariantId];

IF COL_LENGTH('dbo.ProductVariants', 'SortOrder') IS NOT NULL
    ALTER TABLE [ProductVariants] DROP COLUMN [SortOrder];

IF COL_LENGTH('dbo.ProductVariants', 'Price') IS NOT NULL
    ALTER TABLE [ProductVariants] DROP COLUMN [Price];

IF COL_LENGTH('dbo.ProductVariants', 'IsDefault') IS NOT NULL
    ALTER TABLE [ProductVariants] DROP COLUMN [IsDefault];

IF COL_LENGTH('dbo.ProductVariants', 'CompareAtPrice') IS NOT NULL
    ALTER TABLE [ProductVariants] DROP COLUMN [CompareAtPrice];

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_ProductVariants_Products_ProductId')
    ALTER TABLE [ProductVariants] ADD CONSTRAINT [FK_ProductVariants_Products_ProductId]
        FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION;
""");
        }
    }
}
