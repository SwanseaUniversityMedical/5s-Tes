using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Credentials.Models.Migrations
{
    /// <inheritdoc />
    public partial class AddVaultPathColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VaultPath",
                table: "EphemeralCredsReadyMessages",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VaultPath",
                table: "EphemeralCredsReadyMessages");
        }
    }
}
