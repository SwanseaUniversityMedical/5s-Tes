using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agent.Api.Migrations
{
    /// <inheritdoc />
    public partial class bucketCleanupColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BucketsCleaned",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "BucketsCleanedOn",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "BucketsCleaned",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "BucketsCleanedOn",
                table: "Projects");
        }
    }
}
