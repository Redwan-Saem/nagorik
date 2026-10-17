using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUtilityAnomalySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnomalyType",
                table: "Reports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoPaths",
                table: "Reports",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "'[]'");

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNo",
                table: "Reports",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Utility",
                table: "Reports",
                type: "TEXT",
                nullable: true);

            // Backfill after adding the columns, before constraints schedule SQLite's table rebuild.
            migrationBuilder.Sql("""
                UPDATE Reports SET PhotoPaths = json_array(ImageUrl)
                WHERE ImageUrl IS NOT NULL
                  AND length(trim(ImageUrl, char(9) || char(10) || char(13) || ' ')) > 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_AnomalyType_Latitude_Longitude_CreatedAt",
                table: "Reports",
                columns: new[] { "AnomalyType", "Latitude", "Longitude", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReferenceNo",
                table: "Reports",
                column: "ReferenceNo",
                unique: true,
                filter: "\"ReferenceNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Utility_CreatedAt_Id",
                table: "Reports",
                columns: new[] { "Utility", "CreatedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reports_AnomalyType",
                table: "Reports",
                sql: "\"AnomalyType\" IS NULL OR \"AnomalyType\" IN ('ExposedWire', 'GasLeak', 'FallenPole', 'PowerOutage', 'WaterLeak', 'Other')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reports_HighRiskUrgency",
                table: "Reports",
                sql: "\"AnomalyType\" IS NULL OR \"AnomalyType\" NOT IN ('ExposedWire', 'GasLeak', 'FallenPole') OR \"Urgency\" IN (2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reports_PhotoPaths",
                table: "Reports",
                sql: "CASE WHEN json_valid(\"PhotoPaths\") THEN json_type(\"PhotoPaths\") = 'array' AND json_array_length(\"PhotoPaths\") <= 3 AND (json_type(\"PhotoPaths\", '$[0]') IS NULL OR (json_type(\"PhotoPaths\", '$[0]') = 'text' AND length(trim(json_extract(\"PhotoPaths\", '$[0]'), char(9) || char(10) || char(13) || ' ')) > 0)) AND (json_type(\"PhotoPaths\", '$[1]') IS NULL OR (json_type(\"PhotoPaths\", '$[1]') = 'text' AND length(trim(json_extract(\"PhotoPaths\", '$[1]'), char(9) || char(10) || char(13) || ' ')) > 0)) AND (json_type(\"PhotoPaths\", '$[2]') IS NULL OR (json_type(\"PhotoPaths\", '$[2]') = 'text' AND length(trim(json_extract(\"PhotoPaths\", '$[2]'), char(9) || char(10) || char(13) || ' ')) > 0)) ELSE 0 END");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reports_ReferenceNo",
                table: "Reports",
                sql: "\"ReferenceNo\" IS NULL OR (length(\"ReferenceNo\") BETWEEN 9 AND 32 AND substr(\"ReferenceNo\", 1, 3) = 'UA-' AND substr(\"ReferenceNo\", 4) NOT GLOB '*[^0-9]*')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reports_Utility",
                table: "Reports",
                sql: "\"Utility\" IS NULL OR \"Utility\" IN ('Unassigned', 'Desco', 'Dpdc', 'WaterAuthority', 'GasAuthority')");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reports_AnomalyType_Latitude_Longitude_CreatedAt",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_ReferenceNo",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_Utility_CreatedAt_Id",
                table: "Reports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reports_AnomalyType",
                table: "Reports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reports_HighRiskUrgency",
                table: "Reports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reports_PhotoPaths",
                table: "Reports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reports_ReferenceNo",
                table: "Reports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reports_Utility",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "AnomalyType",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "PhotoPaths",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ReferenceNo",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "Utility",
                table: "Reports");
        }
    }
}
