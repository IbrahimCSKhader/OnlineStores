using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryGridStoreTheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Stores_ThemeTemplate",
                table: "Stores");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stores_ThemeTemplate",
                table: "Stores",
                sql: "[ThemeTemplate] IN ('D', 'L', 'F', 'P', 'B', 'U', 'N', 'V', 'Y', 'E', 'R', 'T', 'O', 'C')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [Stores] SET [ThemeTemplate] = 'N' WHERE [ThemeTemplate] = 'C';");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stores_ThemeTemplate",
                table: "Stores");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stores_ThemeTemplate",
                table: "Stores",
                sql: "[ThemeTemplate] IN ('D', 'L', 'F', 'P', 'B', 'U', 'N', 'V', 'Y', 'E', 'R', 'T', 'O')");
        }
    }
}
