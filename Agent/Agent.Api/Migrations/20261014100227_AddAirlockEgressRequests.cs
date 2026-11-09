using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agent.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAirlockEgressRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AirlockEgressRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubId = table.Column<int>(type: "integer", nullable: false),
                    TesId = table.Column<string>(type: "text", nullable: false),
                    TesName = table.Column<string>(type: "text", nullable: true),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    OutputBucket = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    AirlockRequestId = table.Column<int>(type: "integer", nullable: true),
                    AirlockRequestVersion = table.Column<int>(type: "integer", nullable: true),
                    LastAirlockStatus = table.Column<int>(type: "integer", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    SendAttempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirlockEgressRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AirlockEgressRequests_AirlockRequestId",
                table: "AirlockEgressRequests",
                column: "AirlockRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AirlockEgressRequests_State",
                table: "AirlockEgressRequests",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_AirlockEgressRequests_SubId",
                table: "AirlockEgressRequests",
                column: "SubId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AirlockEgressRequests");
        }
    }
}
