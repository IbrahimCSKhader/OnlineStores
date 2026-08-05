using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using onlineStore.Data;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260805120000_AllowBlueStoreThemeTemplate")]
    public partial class AllowBlueStoreThemeTemplate : Migration
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
                sql: "[ThemeTemplate] IN ('D', 'L', 'F', 'P', 'B', 'U')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Stores_ThemeTemplate",
                table: "Stores");

            migrationBuilder.Sql(
                """
                UPDATE [Stores]
                SET [ThemeTemplate] = 'D'
                WHERE [ThemeTemplate] = 'U';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stores_ThemeTemplate",
                table: "Stores",
                sql: "[ThemeTemplate] IN ('D', 'L', 'F', 'P', 'B')");
        }
    }
}
