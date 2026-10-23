using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskAlertDeliveryGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RiskAlertDeliveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<long>(type: "INTEGER", nullable: false),
                    ResidentId = table.Column<int>(type: "INTEGER", nullable: false),
                    ZoneId = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClaimToken = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAlertDeliveries", x => x.Id);
                    table.CheckConstraint("CK_RiskAlertDeliveries_State", "\"Attempts\" BETWEEN 0 AND 100 AND \"State\" IN ('Pending','Sending','Sent','Failed','Suppressed') AND (\"State\" <> 'Sent' OR \"SentAtUtc\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RiskAlertDeliveries_RiskAlertEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "RiskAlertEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskAlertDeliveries_Users_ResidentId",
                        column: x => x.ResidentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskAlertDeliveries_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskAlertProcessorLeases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Token = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAlertProcessorLeases", x => x.Id);
                    table.CheckConstraint("CK_RiskAlertProcessorLeases_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiskAlertDeliveries_EventId_ResidentId",
                table: "RiskAlertDeliveries",
                columns: new[] { "EventId", "ResidentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskAlertDeliveries_ResidentId_ZoneId_SentAtUtc",
                table: "RiskAlertDeliveries",
                columns: new[] { "ResidentId", "ZoneId", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskAlertDeliveries_State_NextAttemptAtUtc",
                table: "RiskAlertDeliveries",
                columns: new[] { "State", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskAlertDeliveries_ZoneId",
                table: "RiskAlertDeliveries",
                column: "ZoneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RiskAlertDeliveries");

            migrationBuilder.DropTable(
                name: "RiskAlertProcessorLeases");
        }
    }
}
