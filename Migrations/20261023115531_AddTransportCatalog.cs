using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTransportCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransportFareRules",
                columns: table => new
                {
                    Mode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    BaseBdt = table.Column<double>(type: "REAL", nullable: false),
                    PerKmBdt = table.Column<double>(type: "REAL", nullable: false),
                    MinimumBdt = table.Column<double>(type: "REAL", nullable: false),
                    UpperMultiplier = table.Column<double>(type: "REAL", nullable: false),
                    IsExact = table.Column<bool>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransportFareRules", x => x.Mode);
                    table.CheckConstraint("CK_TransportFareRules_Values", "BaseBdt BETWEEN 0 AND 10000 AND PerKmBdt BETWEEN 0 AND 10000 AND MinimumBdt BETWEEN 0 AND 10000 AND UpperMultiplier BETWEEN 1 AND 10 AND Mode IN ('Bus','Metro','CNG','Rickshaw','Walk')");
                });

            migrationBuilder.CreateTable(
                name: "TransportStops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsSample = table.Column<bool>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransportStops", x => x.Id);
                    table.CheckConstraint("CK_TransportStops_Coordinates", "Latitude BETWEEN -90 AND 90 AND Longitude BETWEEN -180 AND 180");
                });

            migrationBuilder.CreateTable(
                name: "TransportLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FromStopId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToStopId = table.Column<int>(type: "INTEGER", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Line = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DistanceKm = table.Column<double>(type: "REAL", nullable: false),
                    Minutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsSample = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransportLinks", x => x.Id);
                    table.CheckConstraint("CK_TransportLinks_Values", "FromStopId <> ToStopId AND DistanceKm > 0 AND DistanceKm <= 100 AND Minutes BETWEEN 1 AND 300 AND Mode IN ('Bus','Metro','CNG','Rickshaw','Walk')");
                    table.ForeignKey(
                        name: "FK_TransportLinks_TransportStops_FromStopId",
                        column: x => x.FromStopId,
                        principalTable: "TransportStops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransportLinks_TransportStops_ToStopId",
                        column: x => x.ToStopId,
                        principalTable: "TransportStops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransportLinks_FromStopId_Enabled",
                table: "TransportLinks",
                columns: new[] { "FromStopId", "Enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_TransportLinks_ToStopId",
                table: "TransportLinks",
                column: "ToStopId");

            migrationBuilder.CreateIndex(
                name: "IX_TransportStops_Enabled_Name",
                table: "TransportStops",
                columns: new[] { "Enabled", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransportFareRules");

            migrationBuilder.DropTable(
                name: "TransportLinks");

            migrationBuilder.DropTable(
                name: "TransportStops");
        }
    }
}
