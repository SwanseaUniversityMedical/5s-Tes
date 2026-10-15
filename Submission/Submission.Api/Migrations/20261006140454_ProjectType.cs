using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submission.Api.Migrations
{
    /// <inheritdoc />
    public partial class ProjectType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProjectType",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectType",
                table: "Projects");
        }
    }
}
