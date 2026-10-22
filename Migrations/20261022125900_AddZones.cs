using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Zones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false, collation: "NOCASE"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    BoundaryGeoJson = table.Column<string>(type: "TEXT", nullable: false),
                    RiskLevel = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Unknown"),
                    LastComputedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zones", x => x.Id);
                    table.CheckConstraint("CK_Zones_BoundaryGeoJson", "CASE WHEN json_valid(\"BoundaryGeoJson\") THEN COALESCE(json_extract(\"BoundaryGeoJson\", '$.type') IN ('Polygon', 'MultiPolygon') AND json_type(\"BoundaryGeoJson\", '$.coordinates') = 'array' AND json_array_length(\"BoundaryGeoJson\", '$.coordinates') > 0, 0) ELSE 0 END");
                    table.CheckConstraint("CK_Zones_Code", "length(trim(\"Code\")) BETWEEN 1 AND 40 AND \"Code\" = trim(\"Code\")");
                    table.CheckConstraint("CK_Zones_Name", "length(trim(\"Name\")) BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_Zones_RiskLevel", "\"RiskLevel\" IN ('Unknown', 'Low', 'Medium', 'High', 'Severe')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Zones_Code",
                table: "Zones",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Zones_RiskLevel_LastComputedAtUtc",
                table: "Zones",
                columns: new[] { "RiskLevel", "LastComputedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Zones");
        }
    }
}
