using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Submission.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFederatedTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FederatedTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreId = table.Column<int>(type: "integer", nullable: false),
                    StudyId = table.Column<string>(type: "text", nullable: false),
                    Operation = table.Column<string>(type: "text", nullable: false),
                    Dataset = table.Column<string>(type: "text", nullable: false),
                    TesTaskJson = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResultValue = table.Column<decimal>(type: "numeric", nullable: true),
                    ResultRowCount = table.Column<int>(type: "integer", nullable: true),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ManifestHash = table.Column<string>(type: "text", nullable: true),
                    SiteToken = table.Column<string>(type: "text", nullable: true),
                    ApprovalReference = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FederatedTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FederatedTasks_Tres_TreId",
                        column: x => x.TreId,
                        principalTable: "Tres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FederatedTasks_TaskId",
                table: "FederatedTasks",
                column: "TaskId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FederatedTasks_TreId_State",
                table: "FederatedTasks",
                columns: new[] { "TreId", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FederatedTasks");
        }
    }
}
