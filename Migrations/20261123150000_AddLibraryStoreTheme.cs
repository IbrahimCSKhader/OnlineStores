using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using onlineStore.Data;

#nullable disable

namespace onlineStore.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261123150000_AddLibraryStoreTheme")]
    public partial class AddLibraryStoreTheme : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint("CK_Stores_ThemeTemplate", "Stores");
            migrationBuilder.AddCheckConstraint("CK_Stores_ThemeTemplate", "Stores",
                "[ThemeTemplate] IN ('D', 'L', 'F', 'P', 'B', 'U', 'N', 'V', 'Y', 'E', 'R', 'T', 'O', 'C', 'K')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [Stores] SET [ThemeTemplate] = 'N' WHERE [ThemeTemplate] = 'K';");
            migrationBuilder.DropCheckConstraint("CK_Stores_ThemeTemplate", "Stores");
            migrationBuilder.AddCheckConstraint("CK_Stores_ThemeTemplate", "Stores",
                "[ThemeTemplate] IN ('D', 'L', 'F', 'P', 'B', 'U', 'N', 'V', 'Y', 'E', 'R', 'T', 'O', 'C')");
        }
    }
}
