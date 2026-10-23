using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLoadsheddingSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoadsheddingSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    AvailableCapacityMw = table.Column<double>(type: "REAL", nullable: false),
                    SlotMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PublishedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoadsheddingSchedules", x => x.Id);
                    table.CheckConstraint("CK_LoadsheddingSchedule_Input", "\"AvailableCapacityMw\" > 0 AND \"AvailableCapacityMw\" <= 1000000 AND \"StartDate\" <= \"EndDate\" AND \"SlotMinutes\" IN (60,120,180,240,360) AND \"Revision\" > 0");
                    table.CheckConstraint("CK_LoadsheddingSchedule_State", "\"State\" IN ('Draft','Published','Superseded') AND (\"State\" = 'Draft' OR (\"PublishedByUserId\" IS NOT NULL AND \"PublishedAtUtc\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_LoadsheddingSchedules_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoadsheddingSchedules_Users_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoadsheddingSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScheduleId = table.Column<int>(type: "INTEGER", nullable: false),
                    ZoneId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    StartMinute = table.Column<int>(type: "INTEGER", nullable: false),
                    EndMinute = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoadsheddingSlots", x => x.Id);
                    table.CheckConstraint("CK_LoadsheddingSlot_Time", "\"StartMinute\" >= 0 AND \"EndMinute\" <= 1440 AND \"StartMinute\" < \"EndMinute\"");
                    table.ForeignKey(
                        name: "FK_LoadsheddingSlots_LoadsheddingSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "LoadsheddingSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoadsheddingSlots_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScheduleId = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleAudits_LoadsheddingSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "LoadsheddingSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleAudits_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulePublications",
                columns: table => new
                {
                    ZoneId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ScheduleId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulePublications", x => new { x.ZoneId, x.Date });
                    table.ForeignKey(
                        name: "FK_SchedulePublications_LoadsheddingSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "LoadsheddingSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulePublications_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleZoneDemands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScheduleId = table.Column<int>(type: "INTEGER", nullable: false),
                    ZoneId = table.Column<int>(type: "INTEGER", nullable: false),
                    DemandMw = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleZoneDemands", x => x.Id);
                    table.CheckConstraint("CK_ScheduleZoneDemand_Positive", "\"DemandMw\" > 0 AND \"DemandMw\" <= 1000000");
                    table.ForeignKey(
                        name: "FK_ScheduleZoneDemands_LoadsheddingSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "LoadsheddingSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleZoneDemands_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoadsheddingSchedules_CreatedByUserId",
                table: "LoadsheddingSchedules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LoadsheddingSchedules_PublishedByUserId",
                table: "LoadsheddingSchedules",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LoadsheddingSchedules_State_StartDate_EndDate",
                table: "LoadsheddingSchedules",
                columns: new[] { "State", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LoadsheddingSlots_ScheduleId_ZoneId_Date_StartMinute",
                table: "LoadsheddingSlots",
                columns: new[] { "ScheduleId", "ZoneId", "Date", "StartMinute" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoadsheddingSlots_ZoneId",
                table: "LoadsheddingSlots",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleAudits_ActorUserId",
                table: "ScheduleAudits",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleAudits_ScheduleId_Id",
                table: "ScheduleAudits",
                columns: new[] { "ScheduleId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulePublications_ScheduleId",
                table: "SchedulePublications",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleZoneDemands_ScheduleId_ZoneId",
                table: "ScheduleZoneDemands",
                columns: new[] { "ScheduleId", "ZoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleZoneDemands_ZoneId",
                table: "ScheduleZoneDemands",
                column: "ZoneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoadsheddingSlots");

            migrationBuilder.DropTable(
                name: "ScheduleAudits");

            migrationBuilder.DropTable(
                name: "SchedulePublications");

            migrationBuilder.DropTable(
                name: "ScheduleZoneDemands");

            migrationBuilder.DropTable(
                name: "LoadsheddingSchedules");
        }
    }
}
