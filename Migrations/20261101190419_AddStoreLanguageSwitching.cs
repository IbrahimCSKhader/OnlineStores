using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreLanguageSwitching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableLanguageSwitching",
                table: "Stores",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableLanguageSwitching",
                table: "Stores");
        }
    }
}
