using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCrewAndAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedCrewId",
                table: "Reports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Crews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Crews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Crews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CrewAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReportId = table.Column<int>(type: "INTEGER", nullable: false),
                    CrewId = table.Column<int>(type: "INTEGER", nullable: false),
                    DispatcherId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrewAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrewAssignments_Crews_CrewId",
                        column: x => x.CrewId,
                        principalTable: "Crews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CrewAssignments_Reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_AssignedCrewId",
                table: "Reports",
                column: "AssignedCrewId");

            migrationBuilder.CreateIndex(
                name: "IX_CrewAssignments_CrewId",
                table: "CrewAssignments",
                column: "CrewId");

            migrationBuilder.CreateIndex(
                name: "IX_CrewAssignments_ReportId_AssignedAtUtc",
                table: "CrewAssignments",
                columns: new[] { "ReportId", "AssignedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Crews_UserId",
                table: "Crews",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Crews_AssignedCrewId",
                table: "Reports",
                column: "AssignedCrewId",
                principalTable: "Crews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Crews_AssignedCrewId",
                table: "Reports");

            migrationBuilder.DropTable(
                name: "CrewAssignments");

            migrationBuilder.DropTable(
                name: "Crews");

            migrationBuilder.DropIndex(
                name: "IX_Reports_AssignedCrewId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "AssignedCrewId",
                table: "Reports");
        }
    }
}
