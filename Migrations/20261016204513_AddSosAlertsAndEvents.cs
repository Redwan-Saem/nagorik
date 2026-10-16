using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSosAlertsAndEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SosAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true),
                    AddressText = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ResponderUserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SosAlerts", x => x.Id);
                    table.CheckConstraint("CK_SosAlerts_AddressLength", "length(\"AddressText\") <= 500");
                    table.CheckConstraint("CK_SosAlerts_Coordinates", "(\"Latitude\" IS NULL AND \"Longitude\" IS NULL) OR (\"Latitude\" IS NOT NULL AND \"Longitude\" IS NOT NULL AND \"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180)");
                    table.CheckConstraint("CK_SosAlerts_Location", "(\"Latitude\" IS NOT NULL AND \"Longitude\" IS NOT NULL) OR length(trim(\"AddressText\", char(9) || char(10) || char(13) || ' ')) > 0");
                    table.CheckConstraint("CK_SosAlerts_Status", "\"Status\" IN ('Active', 'Responding', 'Completed', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_SosAlerts_Users_ResponderUserId",
                        column: x => x.ResponderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SosAlerts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SosAlertEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SosAlertId = table.Column<int>(type: "INTEGER", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ActorUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true),
                    AddressText = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SosAlertEvents", x => x.Id);
                    table.CheckConstraint("CK_SosAlertEvents_AddressLength", "length(\"AddressText\") <= 500");
                    table.CheckConstraint("CK_SosAlertEvents_Coordinates", "(\"Latitude\" IS NULL AND \"Longitude\" IS NULL) OR (\"Latitude\" IS NOT NULL AND \"Longitude\" IS NOT NULL AND \"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180)");
                    table.CheckConstraint("CK_SosAlertEvents_EventType", "\"EventType\" IN ('Triggered', 'LocationUpdated', 'ResponderAssigned', 'Completed', 'Cancelled')");
                    table.CheckConstraint("CK_SosAlertEvents_Location", "(\"Latitude\" IS NOT NULL AND \"Longitude\" IS NOT NULL) OR length(trim(\"AddressText\", char(9) || char(10) || char(13) || ' ')) > 0");
                    table.CheckConstraint("CK_SosAlertEvents_Status", "\"Status\" IN ('Active', 'Responding', 'Completed', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_SosAlertEvents_SosAlerts_SosAlertId",
                        column: x => x.SosAlertId,
                        principalTable: "SosAlerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SosAlertEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SosAlertEvents_ActorUserId",
                table: "SosAlertEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SosAlertEvents_SosAlertId_OccurredAtUtc",
                table: "SosAlertEvents",
                columns: new[] { "SosAlertId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SosAlerts_OneActivePerUser",
                table: "SosAlerts",
                column: "UserId",
                unique: true,
                filter: "\"Status\" IN ('Active', 'Responding')");

            migrationBuilder.CreateIndex(
                name: "IX_SosAlerts_ResponderUserId",
                table: "SosAlerts",
                column: "ResponderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SosAlerts_Status_CreatedAtUtc",
                table: "SosAlerts",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SosAlerts_UserId_CreatedAtUtc",
                table: "SosAlerts",
                columns: new[] { "UserId", "CreatedAtUtc" });

            // Audit snapshots are append-only, even for direct SQL callers.
            migrationBuilder.Sql("""
                CREATE TRIGGER TR_SosAlertEvents_NoUpdate
                BEFORE UPDATE ON SosAlertEvents
                BEGIN
                    SELECT RAISE(ABORT, 'SOS alert events are append-only.');
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER TR_SosAlertEvents_NoDelete
                BEFORE DELETE ON SosAlertEvents
                BEGIN
                    SELECT RAISE(ABORT, 'SOS alert events are append-only.');
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SosAlertEvents");

            migrationBuilder.DropTable(
                name: "SosAlerts");
        }
    }
}
