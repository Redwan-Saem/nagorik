using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCrewLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "LastLatitude",
                table: "Crews",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastLongitude",
                table: "Crews",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAtUtc",
                table: "Crews",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Crews_Location",
                table: "Crews",
                sql: "(\"LastLatitude\" IS NULL AND \"LastLongitude\" IS NULL AND \"LastSeenAtUtc\" IS NULL) OR (\"LastLatitude\" IS NOT NULL AND \"LastLongitude\" IS NOT NULL AND \"LastSeenAtUtc\" IS NOT NULL AND \"LastLatitude\" BETWEEN -90 AND 90 AND \"LastLongitude\" BETWEEN -180 AND 180)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Crews_Location",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "LastLatitude",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "LastLongitude",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "LastSeenAtUtc",
                table: "Crews");
        }
    }
}
