using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace onlineStore.Migrations
{
    /// <inheritdoc />
    public partial class RefineStoreContactAccountsToPlatformUsernames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "StoreContactAccounts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE sca
                SET Username = LTRIM(RTRIM(COALESCE(NULLIF(sca.[Value], N''), NULLIF(sca.[Url], N''), N'')))
                FROM StoreContactAccounts sca;

                UPDATE StoreContactAccounts
                SET Username = REPLACE(REPLACE(Username, N'https://www.instagram.com/', N''), N'https://instagram.com/', N'')
                WHERE Platform = N'Instagram';

                UPDATE StoreContactAccounts
                SET Username = REPLACE(REPLACE(Username, N'https://www.tiktok.com/@', N''), N'https://tiktok.com/@', N'')
                WHERE Platform = N'TikTok';

                UPDATE StoreContactAccounts
                SET Username = REPLACE(REPLACE(Username, N'https://www.facebook.com/', N''), N'https://facebook.com/', N'')
                WHERE Platform = N'Facebook';

                UPDATE StoreContactAccounts
                SET Username = REPLACE(REPLACE(Username, N'https://www.snapchat.com/add/', N''), N'https://snapchat.com/add/', N'')
                WHERE Platform = N'Snapchat';

                UPDATE StoreContactAccounts
                SET Username = REPLACE(REPLACE(Username, N'https://wa.me/', N''), N'https://api.whatsapp.com/send?phone=', N'')
                WHERE Platform = N'WhatsApp';

                UPDATE StoreContactAccounts
                SET Username = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(Username, N'@', N''), N'/', N''), N'?', N''), N'&', N''), N'=', N''), N' ', N'')
                WHERE Platform IN (N'Instagram', N'TikTok', N'Facebook', N'Snapchat');

                UPDATE StoreContactAccounts
                SET Username = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(Username, N'+', N''), N'-', N''), N' ', N''), N'(', N''), N')', N''), N'/', N'')
                WHERE Platform = N'WhatsApp';

                UPDATE StoreContactAccounts
                SET Username = CONVERT(nvarchar(36), Id)
                WHERE Username IS NULL OR LTRIM(RTRIM(Username)) = N'';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Platform",
                table: "StoreContactAccounts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "StoreContactAccounts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Url",
                table: "StoreContactAccounts");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "StoreContactAccounts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "StoreContactAccounts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Value",
                table: "StoreContactAccounts",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE StoreContactAccounts
                SET [Value] = Username;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Platform",
                table: "StoreContactAccounts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.DropColumn(
                name: "Username",
                table: "StoreContactAccounts");
        }
    }
}
