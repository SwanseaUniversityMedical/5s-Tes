using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submission.Api.Migrations
{
    /// <inheritdoc />
    public partial class alterlogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserName",
                table: "AuditLogs",
                newName: "LoggedInUserName");

            migrationBuilder.RenameColumn(
                name: "TestaskId",
                table: "AuditLogs",
                newName: "SubmissionId");

            migrationBuilder.RenameColumn(
                name: "FormData",
                table: "AuditLogs",
                newName: "HistoricFormData");

            migrationBuilder.AddColumn<int>(
                name: "LogType",
                table: "AuditLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ProjectId",
                table: "AuditLogs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_SubmissionId",
                table: "AuditLogs",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TreId",
                table: "AuditLogs",
                column: "TreId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Projects_ProjectId",
                table: "AuditLogs",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Submissions_SubmissionId",
                table: "AuditLogs",
                column: "SubmissionId",
                principalTable: "Submissions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Tres_TreId",
                table: "AuditLogs",
                column: "TreId",
                principalTable: "Tres",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Users_UserId",
                table: "AuditLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Projects_ProjectId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Submissions_SubmissionId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Tres_TreId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Users_UserId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ProjectId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_SubmissionId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TreId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "LogType",
                table: "AuditLogs");

            migrationBuilder.RenameColumn(
                name: "SubmissionId",
                table: "AuditLogs",
                newName: "TestaskId");

            migrationBuilder.RenameColumn(
                name: "LoggedInUserName",
                table: "AuditLogs",
                newName: "UserName");

            migrationBuilder.RenameColumn(
                name: "HistoricFormData",
                table: "AuditLogs",
                newName: "FormData");
        }
    }
}
