using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using onlineStore.Data;

namespace onlineStore.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261108215938_EnableAlakhawenCustomerRegistrationPrompt")]
public class EnableAlakhawenCustomerRegistrationPrompt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [Stores]
            SET [EnableCustomerRegistrationPrompt] = CASE
                WHEN [Slug] = N'ma3rad-alakhawen' AND [IsDeleted] = 0 THEN 1
                ELSE 0
            END
            WHERE [EnableCustomerRegistrationPrompt] = 1
                OR ([Slug] = N'ma3rad-alakhawen' AND [IsDeleted] = 0);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve subsequent administrator changes to the setting.
    }
}
