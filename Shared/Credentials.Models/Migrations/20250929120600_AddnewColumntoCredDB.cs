using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Credentials.Models.Migrations
{
    /// <inheritdoc />
    public partial class AddnewColumntoCredDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CredentialType",
                table: "EphemeralCredentials",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CredentialType",
                table: "EphemeralCredentials");
        }
    }
}
