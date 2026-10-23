using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RiskHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ZoneId = table.Column<int>(type: "INTEGER", nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PreviousLevel = table.Column<string>(type: "TEXT", nullable: false),
                    Level = table.Column<string>(type: "TEXT", nullable: false),
                    RuleVersion = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    FactorsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskHistories", x => x.Id);
                    table.CheckConstraint("CK_RiskHistory_Factors", "json_valid(\"FactorsJson\")");
                    table.CheckConstraint("CK_RiskHistory_Levels", "\"Level\" IN ('Unknown','Low','Medium','High','Severe') AND \"PreviousLevel\" IN ('Unknown','Low','Medium','High','Severe')");
                    table.ForeignKey(
                        name: "FK_RiskHistories_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiskHistories_ZoneId_ComputedAtUtc",
                table: "RiskHistories",
                columns: new[] { "ZoneId", "ComputedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RiskHistories");
        }
    }
}
