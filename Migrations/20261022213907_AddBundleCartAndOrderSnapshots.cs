using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    public partial class AddBundleCartAndOrderSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BundleGroupId",
                table: "OrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BundleItemQuantity",
                table: "OrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BundleName",
                table: "OrderItems",
                type: "nvarchar(180)",
                maxLength: 180,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BundleOfferId",
                table: "OrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BundlePrice",
                table: "OrderItems",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BundleQuantity",
                table: "OrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BundleGroupId",
                table: "CartItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BundleItemQuantity",
                table: "CartItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BundleLineTotal",
                table: "CartItems",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BundleName",
                table: "CartItems",
                type: "nvarchar(180)",
                maxLength: 180,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BundleOfferId",
                table: "CartItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BundlePrice",
                table: "CartItems",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BundleQuantity",
                table: "CartItems",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BundleGroupId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "BundleItemQuantity",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "BundleName",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "BundleOfferId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "BundlePrice",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "BundleQuantity",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "BundleGroupId",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "BundleItemQuantity",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "BundleLineTotal",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "BundleName",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "BundleOfferId",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "BundlePrice",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "BundleQuantity",
                table: "CartItems");
        }
    }
}
