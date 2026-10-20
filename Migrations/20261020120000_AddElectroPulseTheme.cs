using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using onlineStore.Data;

#nullable disable

namespace onlineStore.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261020120000_AddElectroPulseTheme")]
    public partial class AddElectroPulseTheme : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint("CK_Stores_ThemeTemplate", "Stores");
            migrationBuilder.AddCheckConstraint(
                "CK_Stores_ThemeTemplate", "Stores",
                "[ThemeTemplate] IN ('D', 'L', 'F', 'P', 'B', 'U', 'N', 'V', 'Y', 'E', 'R', 'T')");

            migrationBuilder.Sql(
                "UPDATE [Stores] SET [ThemeTemplate] = 'T' WHERE [Slug] = 'electropulse-hub';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [Stores] SET [ThemeTemplate] = CASE WHEN [Slug] = 'electropulse-hub' THEN 'U' ELSE 'D' END WHERE [ThemeTemplate] = 'T';");

            migrationBuilder.DropCheckConstraint("CK_Stores_ThemeTemplate", "Stores");
            migrationBuilder.AddCheckConstraint(
                "CK_Stores_ThemeTemplate", "Stores",
                "[ThemeTemplate] IN ('D', 'L', 'F', 'P', 'B', 'U', 'N', 'V', 'Y', 'E', 'R')");
        }
    }
}
