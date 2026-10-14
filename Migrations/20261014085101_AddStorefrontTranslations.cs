using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    public partial class AddStorefrontTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Stores",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "A");

            migrationBuilder.CreateTable(
                name: "StoreTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false, defaultValue: "A"),
                    FieldName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", maxLength: 100000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreTranslations", x => x.Id);
                    table.CheckConstraint("CK_StoreTranslations_Language", "[Language] IN ('A', 'E', 'I')");
                    table.ForeignKey(
                        name: "FK_StoreTranslations_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stores_Language",
                table: "Stores",
                sql: "[Language] IN ('A', 'E', 'I')");

            migrationBuilder.CreateIndex(
                name: "IX_StoreTranslations_StoreId_EntityType_EntityId_Language_FieldName",
                table: "StoreTranslations",
                columns: new[] { "StoreId", "EntityType", "EntityId", "Language", "FieldName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoreTranslations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stores_Language",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Stores");
        }
    }
}
