using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAccountLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountCustomerId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AccountInitialized",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasSharedPhone",
                table: "CustomerStores",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsGuestProfile",
                table: "CustomerStores",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "MergedIntoCustomerId",
                table: "CustomerStores",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPhone",
                table: "CustomerStores",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerAccountAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAccountAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Method = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReversesEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerLedgerEntries_CustomerStores_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerStores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerLedgerEntries_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPaymentAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentAllocations_CustomerLedgerEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "CustomerLedgerEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentAllocations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_AccountCustomerId",
                table: "Orders",
                column: "AccountCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerStores_StoreId_NormalizedPhone",
                table: "CustomerStores",
                columns: new[] { "StoreId", "NormalizedPhone" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerEntries_CustomerId",
                table: "CustomerLedgerEntries",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerEntries_OrderId",
                table: "CustomerLedgerEntries",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerEntries_ReversesEntryId",
                table: "CustomerLedgerEntries",
                column: "ReversesEntryId",
                unique: true,
                filter: "[ReversesEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerEntries_StoreId_CustomerId_OccurredAt",
                table: "CustomerLedgerEntries",
                columns: new[] { "StoreId", "CustomerId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerEntries_StoreId_RequestKey",
                table: "CustomerLedgerEntries",
                columns: new[] { "StoreId", "RequestKey" },
                unique: true,
                filter: "[RequestKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentAllocations_EntryId_OrderId",
                table: "CustomerPaymentAllocations",
                columns: new[] { "EntryId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentAllocations_OrderId",
                table: "CustomerPaymentAllocations",
                column: "OrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_CustomerStores_AccountCustomerId",
                table: "Orders",
                column: "AccountCustomerId",
                principalTable: "CustomerStores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_CustomerStores_AccountCustomerId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "CustomerAccountAudits");

            migrationBuilder.DropTable(
                name: "CustomerPaymentAllocations");

            migrationBuilder.DropTable(
                name: "CustomerLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_Orders_AccountCustomerId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_CustomerStores_StoreId_NormalizedPhone",
                table: "CustomerStores");

            migrationBuilder.DropColumn(
                name: "AccountCustomerId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AccountInitialized",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "HasSharedPhone",
                table: "CustomerStores");

            migrationBuilder.DropColumn(
                name: "IsGuestProfile",
                table: "CustomerStores");

            migrationBuilder.DropColumn(
                name: "MergedIntoCustomerId",
                table: "CustomerStores");

            migrationBuilder.DropColumn(
                name: "NormalizedPhone",
                table: "CustomerStores");
        }
    }
}
