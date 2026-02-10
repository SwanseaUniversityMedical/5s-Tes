using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Submission.Api.Migrations
{
    /// <inheritdoc />
    public partial class MembershipTreDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MembershipTreDecisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubmissionProjId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    TreId = table.Column<int>(type: "integer", nullable: true),
                    Decision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipTreDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembershipTreDecisions_Projects_SubmissionProjId",
                        column: x => x.SubmissionProjId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MembershipTreDecisions_Tres_TreId",
                        column: x => x.TreId,
                        principalTable: "Tres",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MembershipTreDecisions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipTreDecisions_SubmissionProjId",
                table: "MembershipTreDecisions",
                column: "SubmissionProjId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipTreDecisions_TreId",
                table: "MembershipTreDecisions",
                column: "TreId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipTreDecisions_UserId",
                table: "MembershipTreDecisions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MembershipTreDecisions");
        }
    }
}
