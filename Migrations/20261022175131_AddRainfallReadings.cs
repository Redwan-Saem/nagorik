using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRainfallReadings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RainfallReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ZoneId = table.Column<int>(type: "INTEGER", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsForecast = table.Column<bool>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Rainfall1HourMm = table.Column<double>(type: "REAL", nullable: false),
                    Rainfall3HourMm = table.Column<double>(type: "REAL", nullable: false),
                    Rainfall6HourMm = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RainfallReadings", x => x.Id);
                    table.CheckConstraint("CK_RainfallReadings_Amounts", "\"Rainfall1HourMm\" >= 0 AND \"Rainfall3HourMm\" >= \"Rainfall1HourMm\" AND \"Rainfall6HourMm\" >= \"Rainfall3HourMm\" AND \"Rainfall6HourMm\" <= 10000");
                    table.CheckConstraint("CK_RainfallReadings_Forecast", "\"IsForecast\" IN (0, 1)");
                    table.CheckConstraint("CK_RainfallReadings_Source", "length(trim(\"Source\")) BETWEEN 1 AND 64");
                    table.ForeignKey(
                        name: "FK_RainfallReadings_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RainfallReadings_ZoneId_IsForecast_TimestampUtc",
                table: "RainfallReadings",
                columns: new[] { "ZoneId", "IsForecast", "TimestampUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_RainfallReadings_ZoneId_TimestampUtc_IsForecast_Source",
                table: "RainfallReadings",
                columns: new[] { "ZoneId", "TimestampUtc", "IsForecast", "Source" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RainfallReadings");
        }
    }
}
