using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulseLink.Infrastructure.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class HandoffReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_IncidentId_Action",
                table: "AuditEvents",
                columns: new[] { "IncidentId", "Action" },
                unique: true,
                filter: "[IncidentId] IS NOT NULL AND [Action] = 'HandoffAcknowledged'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_IncidentId_Action",
                table: "AuditEvents");
        }
    }
}
