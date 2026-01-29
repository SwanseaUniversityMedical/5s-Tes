using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Credentials.Models.Migrations
{
    /// <inheritdoc />
    public partial class AddnewparentColumntoCredDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ParentProcessInstanceKey",
                table: "EphemeralCredentials",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentProcessInstanceKey",
                table: "EphemeralCredentials");
        }
    }
}
