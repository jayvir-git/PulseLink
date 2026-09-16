using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulseLink.Infrastructure.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddInterventionOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterventionOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Key = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InterventionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterventionOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterventionOperations_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InterventionOperations_Interventions_InterventionId",
                        column: x => x.InterventionId,
                        principalTable: "Interventions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterventionOperations_ActorUserId_IncidentId_Key",
                table: "InterventionOperations",
                columns: new[] { "ActorUserId", "IncidentId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterventionOperations_IncidentId",
                table: "InterventionOperations",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_InterventionOperations_InterventionId",
                table: "InterventionOperations",
                column: "InterventionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterventionOperations");
        }
    }
}
