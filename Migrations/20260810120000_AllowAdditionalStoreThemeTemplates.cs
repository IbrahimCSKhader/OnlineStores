using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using onlineStore.Data;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260810120000_AllowAdditionalStoreThemeTemplates")]
    public partial class AllowAdditionalStoreThemeTemplates : Migration
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
                sql: "[ThemeTemplate] IN ('D', 'L', 'F', 'P', 'B', 'U', 'N', 'V', 'Y', 'E', 'R')");
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
                WHERE [ThemeTemplate] IN ('N', 'V', 'Y', 'E', 'R');
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stores_ThemeTemplate",
                table: "Stores",
                sql: "[ThemeTemplate] IN ('D', 'L', 'F', 'P', 'B', 'U')");
        }
    }
}
