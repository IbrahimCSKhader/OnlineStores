using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchasePoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PointsEarned",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PurchasePoints",
                table: "CustomerStores",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE o
                SET PointsEarned = ISNULL(items.PointsEarned, 0)
                FROM Orders o
                OUTER APPLY (
                    SELECT SUM(CASE WHEN oi.Quantity > 0 THEN oi.Quantity ELSE 0 END) * 5 AS PointsEarned
                    FROM OrderItems oi
                    WHERE oi.OrderId = o.Id
                ) items;
                """);

            migrationBuilder.Sql(
                """
                UPDATE c
                SET PurchasePoints = ISNULL(points.PurchasePoints, 0)
                FROM CustomerStores c
                OUTER APPLY (
                    SELECT SUM(o.PointsEarned) AS PurchasePoints
                    FROM Orders o
                    WHERE o.StoreCustomerId = c.Id
                ) points;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PointsEarned",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PurchasePoints",
                table: "CustomerStores");
        }
    }
}
