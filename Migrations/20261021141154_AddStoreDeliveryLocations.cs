using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using onlineStore.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20261021141154_AddStoreDeliveryLocations")]
    public partial class AddStoreDeliveryLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryCost",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryLocationId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryLocationName",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "افتراضي");

            migrationBuilder.CreateTable(
                name: "StoreDeliveryLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreDeliveryLocations", x => x.Id);
                    table.CheckConstraint("CK_StoreDeliveryLocations_Cost", "[Cost] >= 0");
                    table.ForeignKey(
                        name: "FK_StoreDeliveryLocations_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DeliveryLocationId",
                table: "Orders",
                column: "DeliveryLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreDeliveryLocations_StoreId",
                table: "StoreDeliveryLocations",
                column: "StoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_StoreDeliveryLocations_DeliveryLocationId",
                table: "Orders",
                column: "DeliveryLocationId",
                principalTable: "StoreDeliveryLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_StoreDeliveryLocations_DeliveryLocationId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "StoreDeliveryLocations");

            migrationBuilder.DropIndex(
                name: "IX_Orders_DeliveryLocationId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryCost",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryLocationId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryLocationName",
                table: "Orders");
        }
    }
}
