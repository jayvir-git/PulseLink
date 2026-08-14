using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulseLink.Infrastructure.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddListAccessIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Incidents_AgencyId",
                table: "Incidents");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Agency_UpdatedAtUtc",
                table: "Incidents",
                columns: new[] { "AgencyId", "UpdatedAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_CreatedBy_UpdatedAtUtc",
                table: "Incidents",
                columns: new[] { "CreatedByUserId", "UpdatedAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_HospitalList",
                table: "Incidents",
                columns: new[] { "DestinationHospitalId", "UpdatedAtUtc", "Id" },
                descending: new[] { false, true, true },
                filter: "Status IN (3, 4, 5)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Incidents_Agency_UpdatedAtUtc",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_CreatedBy_UpdatedAtUtc",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_HospitalList",
                table: "Incidents");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_AgencyId",
                table: "Incidents",
                column: "AgencyId");
        }
    }
}
