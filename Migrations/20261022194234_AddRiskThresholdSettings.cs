using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskThresholdSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RiskThresholdSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediumThreshold = table.Column<double>(type: "REAL", nullable: false),
                    HighThreshold = table.Column<double>(type: "REAL", nullable: false),
                    SevereThreshold = table.Column<double>(type: "REAL", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskThresholdSettings", x => x.Id);
                    table.CheckConstraint("CK_RiskThresholdSettings_Order", "\"MediumThreshold\" > 0 AND \"MediumThreshold\" < \"HighThreshold\" AND \"HighThreshold\" < \"SevereThreshold\" AND \"SevereThreshold\" <= 10000");
                    table.CheckConstraint("CK_RiskThresholdSettings_Singleton", "\"Id\" = 1 AND \"Revision\" > 0");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RiskThresholdSettings");
        }
    }
}
