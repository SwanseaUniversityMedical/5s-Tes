using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agent.Api.Migrations
{
    /// <inheritdoc />
    public partial class missingexpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProjectExpiryDate",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectExpiryDate",
                table: "Projects");
        }
    }
}
