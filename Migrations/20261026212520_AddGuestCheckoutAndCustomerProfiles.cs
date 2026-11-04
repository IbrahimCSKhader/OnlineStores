using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestCheckoutAndCustomerProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequireCustomerLoginForCheckout",
                table: "Stores",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StoreCustomerId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "CustomerEmail",
                table: "Orders",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "CustomerStores",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "CustomerStores",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [Orders] WHERE [StoreCustomerId] IS NULL) THROW 50001, 'Cannot roll back while guest orders exist.', 1;");
            migrationBuilder.DropColumn(
                name: "RequireCustomerLoginForCheckout",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "CustomerEmail",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "CustomerStores");

            migrationBuilder.DropColumn(
                name: "City",
                table: "CustomerStores");

            migrationBuilder.AlterColumn<Guid>(
                name: "StoreCustomerId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
