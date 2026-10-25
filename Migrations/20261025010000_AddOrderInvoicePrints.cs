using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using onlineStore.Data;

namespace onlineStore.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261025010000_AddOrderInvoicePrints")]
public class AddOrderInvoicePrints : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "OrderInvoicePrints", columns: table => new
        {
            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            PrintedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_OrderInvoicePrints", x => x.Id);
            table.ForeignKey("FK_OrderInvoicePrints_Orders_OrderId", x => x.OrderId, "Orders", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_OrderInvoicePrints_OrderId_CreatedAt", table: "OrderInvoicePrints", columns: new[] { "OrderId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("OrderInvoicePrints");
}
