using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using onlineStore.Data;

namespace onlineStore.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261026214000_AllowAnimalKingdomGuestCheckout")]
public class AllowAnimalKingdomGuestCheckout : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [Stores]
            SET [RequireCustomerLoginForCheckout] = 0
            WHERE [Slug] = N'animal-kingdom' AND [IsDeleted] = 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve the requested store policy and any later administrator edits.
    }
}
