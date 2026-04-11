using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    public partial class ConstrainStoreThemeTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [Stores]
                SET [ThemeTemplate] = UPPER(LTRIM(RTRIM([ThemeTemplate])))
                WHERE [ThemeTemplate] IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE [Stores]
                SET [ThemeTemplate] = 'D'
                WHERE [ThemeTemplate] IS NULL
                   OR [ThemeTemplate] NOT IN ('D', 'L', 'F');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ThemeTemplate",
                table: "Stores",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "D",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stores_ThemeTemplate",
                table: "Stores",
                sql: "[ThemeTemplate] IN ('D', 'L', 'F')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Stores_ThemeTemplate",
                table: "Stores");

            migrationBuilder.AlterColumn<string>(
                name: "ThemeTemplate",
                table: "Stores",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1)",
                oldMaxLength: 1,
                oldDefaultValue: "D");
        }
    }
}
