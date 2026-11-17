using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreFavoritesAndRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Products_StoreId_Id",
                table: "Products",
                columns: new[] { "StoreId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CustomerStores_StoreId_Id",
                table: "CustomerStores",
                columns: new[] { "StoreId", "Id" });

            migrationBuilder.CreateTable(
                name: "StoreFavorites",
                columns: table => new
                {
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreFavorites", x => new { x.StoreId, x.CustomerId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_StoreFavorites_CustomerStores_StoreId_CustomerId",
                        columns: x => new { x.StoreId, x.CustomerId },
                        principalTable: "CustomerStores",
                        principalColumns: new[] { "StoreId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StoreFavorites_Products_StoreId_ProductId",
                        columns: x => new { x.StoreId, x.ProductId },
                        principalTable: "Products",
                        principalColumns: new[] { "StoreId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoreRatings",
                columns: table => new
                {
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsGuest = table.Column<bool>(type: "bit", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreRatings", x => new { x.StoreId, x.IsGuest, x.VoterId });
                    table.CheckConstraint("CK_StoreRatings_Score", "[Score] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_StoreRatings_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoreFavorites_StoreId_ProductId",
                table: "StoreFavorites",
                columns: new[] { "StoreId", "ProductId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoreFavorites");

            migrationBuilder.DropTable(
                name: "StoreRatings");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Products_StoreId_Id",
                table: "Products");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CustomerStores_StoreId_Id",
                table: "CustomerStores");
        }
    }
}
