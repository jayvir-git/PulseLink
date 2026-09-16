using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulseLink.Infrastructure.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddIncidentVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "Incidents",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            // Match Microsoft.Data.Sqlite's uppercase GUID parameter encoding so
            // the first UPDATE can match the backfilled concurrency token.
            migrationBuilder.Sql("""
                UPDATE "Incidents" SET "Version" = upper(
                    hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' ||
                    hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6)));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "Incidents");
        }
    }
}
