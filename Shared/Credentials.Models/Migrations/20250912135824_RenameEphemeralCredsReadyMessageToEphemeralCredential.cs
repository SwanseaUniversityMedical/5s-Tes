using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Credentials.Models.Migrations
{
    /// <inheritdoc />
    public partial class RenameEphemeralCredsReadyMessageToEphemeralCredential : Migration
    {
       
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "EphemeralCredsReadyMessages",
                newName: "EphemeralCredentials");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "EphemeralCredentials",
                newName: "EphemeralCredsReadyMessages");
        }
    }
}
