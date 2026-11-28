using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using onlineStore.Data;

#nullable disable
namespace onlineStore.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261128120000_AddAlaaStoreTheme")]
    public partial class AddAlaaStoreTheme : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint("CK_Stores_ThemeTemplate", "Stores");
            migrationBuilder.AddCheckConstraint("CK_Stores_ThemeTemplate", "Stores",
                "[ThemeTemplate] IN ('D', 'L', 'F', 'P', 'B', 'U', 'N', 'V', 'Y', 'E', 'R', 'T', 'O', 'C', 'K', 'A')");
            migrationBuilder.Sql("UPDATE [Stores] SET [ThemeTemplate] = 'A' WHERE [Id] = '0d77c4bc-cfd6-45ad-9088-e8cf3e4213cf' AND [Slug] = 'alaa-rob' AND [ThemeTemplate] = 'K';");
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [Stores] SET [ThemeTemplate] = 'K' WHERE [ThemeTemplate] = 'A';");
            migrationBuilder.DropCheckConstraint("CK_Stores_ThemeTemplate", "Stores");
            migrationBuilder.AddCheckConstraint("CK_Stores_ThemeTemplate", "Stores",
                "[ThemeTemplate] IN ('D', 'L', 'F', 'P', 'B', 'U', 'N', 'V', 'Y', 'E', 'R', 'T', 'O', 'C', 'K')");
        }
    }
}
