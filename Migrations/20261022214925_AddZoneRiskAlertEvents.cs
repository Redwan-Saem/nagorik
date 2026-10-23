using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneRiskAlertEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RiskAlertEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RiskHistoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAlertEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskAlertEvents_RiskHistories_RiskHistoryId",
                        column: x => x.RiskHistoryId,
                        principalTable: "RiskHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiskAlertEvents_ProcessedAtUtc_Id",
                table: "RiskAlertEvents",
                columns: new[] { "ProcessedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskAlertEvents_RiskHistoryId",
                table: "RiskAlertEvents",
                column: "RiskHistoryId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RiskAlertEvents");
        }
    }
}
