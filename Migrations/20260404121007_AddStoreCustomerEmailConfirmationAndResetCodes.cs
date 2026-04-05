using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreCustomerEmailConfirmationAndResetCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailConfirmed",
                table: "CustomerStores",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerificationCodeExpiresAt",
                table: "CustomerStores",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailVerificationCodeHash",
                table: "CustomerStores",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetCodeExpiresAt",
                table: "CustomerStores",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetCodeHash",
                table: "CustomerStores",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailConfirmed",
                table: "CustomerStores");

            migrationBuilder.DropColumn(
                name: "EmailVerificationCodeExpiresAt",
                table: "CustomerStores");

            migrationBuilder.DropColumn(
                name: "EmailVerificationCodeHash",
                table: "CustomerStores");

            migrationBuilder.DropColumn(
                name: "PasswordResetCodeExpiresAt",
                table: "CustomerStores");

            migrationBuilder.DropColumn(
                name: "PasswordResetCodeHash",
                table: "CustomerStores");
        }
    }
}
