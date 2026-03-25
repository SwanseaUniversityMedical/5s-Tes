using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Submission.Api.Migrations
{
    /// <inheritdoc />
    public partial class projecttredecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectTreDecisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubmissionProjId = table.Column<int>(type: "integer", nullable: true),
                    TreId = table.Column<int>(type: "integer", nullable: true),
                    Decision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTreDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTreDecisions_Projects_SubmissionProjId",
                        column: x => x.SubmissionProjId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectTreDecisions_Tres_TreId",
                        column: x => x.TreId,
                        principalTable: "Tres",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTreDecisions_SubmissionProjId",
                table: "ProjectTreDecisions",
                column: "SubmissionProjId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTreDecisions_TreId",
                table: "ProjectTreDecisions",
                column: "TreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectTreDecisions");
        }
    }
}
