using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    public partial class fixS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Sections");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Sections",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
