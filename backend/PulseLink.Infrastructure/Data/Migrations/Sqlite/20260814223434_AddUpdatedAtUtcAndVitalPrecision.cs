using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulseLink.Infrastructure.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddUpdatedAtUtcAndVitalPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Incidents",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // SQLite stores DateTimeOffset as TEXT; copy the UTC instant into UpdatedAtUtc
            // so ORDER BY is valid SQL. SpO2/TemperatureC precision is a no-op here (still TEXT).
            migrationBuilder.Sql("""
                UPDATE "Incidents"
                SET "UpdatedAtUtc" = strftime('%Y-%m-%d %H:%M:%f', "UpdatedAt");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Incidents");
        }
    }
}
