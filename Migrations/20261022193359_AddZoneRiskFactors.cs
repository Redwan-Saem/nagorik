using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneRiskFactors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RiskFactorsJson",
                table: "Zones",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Zones_RiskFactors",
                table: "Zones",
                sql: "\"RiskFactorsJson\" IS NULL OR json_valid(\"RiskFactorsJson\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Zones_RiskFactors",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "RiskFactorsJson",
                table: "Zones");
        }
    }
}
