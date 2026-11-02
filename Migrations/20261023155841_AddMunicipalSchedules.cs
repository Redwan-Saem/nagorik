using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMunicipalSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MunicipalSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    ZoneId = table.Column<int>(type: "INTEGER", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DaysMask = table.Column<int>(type: "INTEGER", nullable: false),
                    StartMinute = table.Column<int>(type: "INTEGER", nullable: false),
                    EndMinute = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MunicipalSchedules", x => x.Id);
                    table.CheckConstraint("CK_MunicipalSchedule_Publication", "State <> 'Published' OR PublishedAtUtc IS NOT NULL");
                    table.CheckConstraint("CK_MunicipalSchedule_Values", "Kind IN ('Water','Waste') AND State IN ('Draft','Published','Retired') AND Revision > 0 AND DaysMask BETWEEN 1 AND 127 AND StartMinute BETWEEN 0 AND 1439 AND EndMinute > StartMinute AND EndMinute <= 1440 AND ToDate >= FromDate");
                    table.ForeignKey(
                        name: "FK_MunicipalSchedules_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MunicipalSchedules_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalSchedules_Kind_ZoneId_State_FromDate_ToDate",
                table: "MunicipalSchedules",
                columns: new[] { "Kind", "ZoneId", "State", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalSchedules_UpdatedByUserId",
                table: "MunicipalSchedules",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalSchedules_ZoneId",
                table: "MunicipalSchedules",
                column: "ZoneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MunicipalSchedules");
        }
    }
}
