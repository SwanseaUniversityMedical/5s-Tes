using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agent.Api.Migrations
{
    /// <inheritdoc />
    public partial class alterlogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Decision",
                table: "TreAuditLogs");

            migrationBuilder.DropColumn(
                name: "TesId",
                table: "TokensToExpire");

            migrationBuilder.AddColumn<bool>(
                name: "Approved",
                table: "TreAuditLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MembershipDecisionId",
                table: "TreAuditLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjectId",
                table: "TreAuditLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubId",
                table: "TokensToExpire",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProjectExpiryDate",
                table: "MembershipDecisions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_TreAuditLogs_MembershipDecisionId",
                table: "TreAuditLogs",
                column: "MembershipDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_TreAuditLogs_ProjectId",
                table: "TreAuditLogs",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_TreAuditLogs_MembershipDecisions_MembershipDecisionId",
                table: "TreAuditLogs",
                column: "MembershipDecisionId",
                principalTable: "MembershipDecisions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TreAuditLogs_Projects_ProjectId",
                table: "TreAuditLogs",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TreAuditLogs_MembershipDecisions_MembershipDecisionId",
                table: "TreAuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_TreAuditLogs_Projects_ProjectId",
                table: "TreAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_TreAuditLogs_MembershipDecisionId",
                table: "TreAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_TreAuditLogs_ProjectId",
                table: "TreAuditLogs");

            migrationBuilder.DropColumn(
                name: "Approved",
                table: "TreAuditLogs");

            migrationBuilder.DropColumn(
                name: "MembershipDecisionId",
                table: "TreAuditLogs");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "TreAuditLogs");

            migrationBuilder.DropColumn(
                name: "SubId",
                table: "TokensToExpire");

            migrationBuilder.DropColumn(
                name: "ProjectExpiryDate",
                table: "MembershipDecisions");

            migrationBuilder.AddColumn<string>(
                name: "Decision",
                table: "TreAuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TesId",
                table: "TokensToExpire",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
