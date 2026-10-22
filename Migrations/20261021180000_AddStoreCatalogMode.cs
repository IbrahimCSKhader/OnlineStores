using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using onlineStore.Data;

namespace onlineStore.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261021180000_AddStoreCatalogMode")]
    public class AddStoreCatalogMode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCatalog", table: "Stores", type: "bit",
                nullable: false, defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsCatalog", table: "Stores");
        }
    }
}
