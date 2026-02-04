using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Credentials.Models.Migrations
{
    /// <inheritdoc />
    public partial class ChangeSubmissionIdToInt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old column entirely (loses all data)
            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "EphemeralCredsReadyMessages");

            // Add the new integer column
            migrationBuilder.AddColumn<int>(
                name: "SubmissionId",
                table: "EphemeralCredsReadyMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Recreate index
            migrationBuilder.CreateIndex(
                name: "IX_EphemeralCredsReadyMessages_SubmissionId_ProcessInstanceKey",
                table: "EphemeralCredsReadyMessages",
                columns: new[] { "SubmissionId", "ProcessInstanceKey" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EphemeralCredsReadyMessages_SubmissionId_ProcessInstanceKey",
                table: "EphemeralCredsReadyMessages");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "EphemeralCredsReadyMessages");

            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionId",
                table: "EphemeralCredsReadyMessages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }
    }
}
