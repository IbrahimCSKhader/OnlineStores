using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    public partial class StoreSpecificCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Carts_Users_UserId",
                table: "Carts");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerStores_Users_CustomerId",
                table: "CustomerStores");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Users_UserId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Users_UserId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_CustomerStores_CustomerId",
                table: "CustomerStores");

            migrationBuilder.DropIndex(
                name: "IX_CustomerStores_StoreId_CustomerId",
                table: "CustomerStores");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Reviews",
                newName: "StoreCustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_Reviews_UserId_ProductId",
                table: "Reviews",
                newName: "IX_Reviews_StoreCustomerId_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_Reviews_UserId",
                table: "Reviews",
                newName: "IX_Reviews_StoreCustomerId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Orders",
                newName: "StoreCustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_Orders_UserId",
                table: "Orders",
                newName: "IX_Orders_StoreCustomerId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Carts",
                newName: "StoreCustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_Carts_UserId",
                table: "Carts",
                newName: "IX_Carts_StoreCustomerId");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "CustomerStores",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "CustomerStores",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "CustomerStores",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "CustomerStores",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "CustomerStores",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE cs
                SET
                    cs.FirstName = COALESCE(u.FirstName, N''),
                    cs.LastName = COALESCE(u.LastName, N''),
                    cs.Email = CASE
                        WHEN u.Email IS NULL OR LTRIM(RTRIM(u.Email)) = N'' THEN CONVERT(nvarchar(36), u.Id) + N'@legacy.local'
                        ELSE LOWER(u.Email)
                    END,
                    cs.Phone = NULLIF(LTRIM(RTRIM(u.PhoneNumber)), N''),
                    cs.PasswordHash = COALESCE(u.PasswordHash, N''),
                    cs.IsActive = CASE WHEN u.IsActive = 1 THEN CAST(1 AS bit) ELSE cs.IsActive END,
                    cs.IsDeleted = 0
                FROM CustomerStores cs
                INNER JOIN Users u ON u.Id = cs.CustomerId;

                ;WITH ReferencedCustomers AS
                (
                    SELECT DISTINCT c.StoreId, c.StoreCustomerId AS UserId
                    FROM Carts c
                    UNION
                    SELECT DISTINCT o.StoreId, o.StoreCustomerId AS UserId
                    FROM Orders o
                    UNION
                    SELECT DISTINCT r.StoreId, r.StoreCustomerId AS UserId
                    FROM Reviews r
                )
                INSERT INTO CustomerStores
                (
                    Id,
                    StoreId,
                    FirstName,
                    LastName,
                    Email,
                    Phone,
                    PasswordHash,
                    DiscountPercentage,
                    IsActive,
                    CreatedAt,
                    UpdatedAt,
                    IsDeleted,
                    CustomerId
                )
                SELECT
                    NEWID(),
                    rc.StoreId,
                    COALESCE(u.FirstName, N''),
                    COALESCE(u.LastName, N''),
                    CASE
                        WHEN u.Email IS NULL OR LTRIM(RTRIM(u.Email)) = N'' THEN CONVERT(nvarchar(36), u.Id) + N'@legacy.local'
                        ELSE LOWER(u.Email)
                    END,
                    NULLIF(LTRIM(RTRIM(u.PhoneNumber)), N''),
                    COALESCE(u.PasswordHash, N''),
                    CAST(0 AS decimal(5,2)),
                    CASE WHEN u.IsActive = 1 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
                    COALESCE(u.CreatedAt, SYSUTCDATETIME()),
                    u.UpdatedAt,
                    CAST(0 AS bit),
                    u.Id
                FROM ReferencedCustomers rc
                INNER JOIN Users u ON u.Id = rc.UserId
                LEFT JOIN CustomerStores cs
                    ON cs.StoreId = rc.StoreId
                   AND cs.CustomerId = rc.UserId
                WHERE cs.Id IS NULL;

                UPDATE c
                SET c.StoreCustomerId = cs.Id
                FROM Carts c
                INNER JOIN CustomerStores cs
                    ON cs.StoreId = c.StoreId
                   AND cs.CustomerId = c.StoreCustomerId;

                UPDATE o
                SET o.StoreCustomerId = cs.Id
                FROM Orders o
                INNER JOIN CustomerStores cs
                    ON cs.StoreId = o.StoreId
                   AND cs.CustomerId = o.StoreCustomerId;

                UPDATE r
                SET r.StoreCustomerId = cs.Id
                FROM Reviews r
                INNER JOIN CustomerStores cs
                    ON cs.StoreId = r.StoreId
                   AND cs.CustomerId = r.StoreCustomerId;
                """);

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "CustomerStores");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerStores_StoreId",
                table: "CustomerStores",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerStores_StoreId_Email",
                table: "CustomerStores",
                columns: new[] { "StoreId", "Email" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Carts_CustomerStores_StoreCustomerId",
                table: "Carts",
                column: "StoreCustomerId",
                principalTable: "CustomerStores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_CustomerStores_StoreCustomerId",
                table: "Orders",
                column: "StoreCustomerId",
                principalTable: "CustomerStores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_CustomerStores_StoreCustomerId",
                table: "Reviews",
                column: "StoreCustomerId",
                principalTable: "CustomerStores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Carts_CustomerStores_StoreCustomerId",
                table: "Carts");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_CustomerStores_StoreCustomerId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_CustomerStores_StoreCustomerId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_CustomerStores_StoreId",
                table: "CustomerStores");

            migrationBuilder.DropIndex(
                name: "IX_CustomerStores_StoreId_Email",
                table: "CustomerStores");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "CustomerStores");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "CustomerStores");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "CustomerStores");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "CustomerStores");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "CustomerStores");

            migrationBuilder.RenameColumn(
                name: "StoreCustomerId",
                table: "Reviews",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Reviews_StoreCustomerId_ProductId",
                table: "Reviews",
                newName: "IX_Reviews_UserId_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_Reviews_StoreCustomerId",
                table: "Reviews",
                newName: "IX_Reviews_UserId");

            migrationBuilder.RenameColumn(
                name: "StoreCustomerId",
                table: "Orders",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Orders_StoreCustomerId",
                table: "Orders",
                newName: "IX_Orders_UserId");

            migrationBuilder.RenameColumn(
                name: "StoreCustomerId",
                table: "Carts",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Carts_StoreCustomerId",
                table: "Carts",
                newName: "IX_Carts_UserId");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "CustomerStores",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_CustomerStores_CustomerId",
                table: "CustomerStores",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerStores_StoreId_CustomerId",
                table: "CustomerStores",
                columns: new[] { "StoreId", "CustomerId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Carts_Users_UserId",
                table: "Carts",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerStores_Users_CustomerId",
                table: "CustomerStores",
                column: "CustomerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Users_UserId",
                table: "Orders",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Users_UserId",
                table: "Reviews",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
