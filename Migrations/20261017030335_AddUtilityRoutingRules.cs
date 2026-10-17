using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUtilityRoutingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UtilityRoutingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Utility = table.Column<string>(type: "TEXT", nullable: false),
                    AnomalyType = table.Column<string>(type: "TEXT", nullable: true),
                    AreaId = table.Column<int>(type: "INTEGER", nullable: true),
                    MinLatitude = table.Column<double>(type: "REAL", nullable: true),
                    MaxLatitude = table.Column<double>(type: "REAL", nullable: true),
                    MinLongitude = table.Column<double>(type: "REAL", nullable: true),
                    MaxLongitude = table.Column<double>(type: "REAL", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityRoutingRules", x => x.Id);
                    table.CheckConstraint("CK_UtilityRoutingRules_AnomalyType", "\"AnomalyType\" IS NULL OR \"AnomalyType\" IN ('ExposedWire', 'GasLeak', 'FallenPole', 'PowerOutage', 'WaterLeak', 'Other')");
                    table.CheckConstraint("CK_UtilityRoutingRules_Bounds", "(\"MinLatitude\" IS NULL AND \"MaxLatitude\" IS NULL AND \"MinLongitude\" IS NULL AND \"MaxLongitude\" IS NULL)\nOR (\"MinLatitude\" IS NOT NULL AND \"MaxLatitude\" IS NOT NULL AND \"MinLongitude\" IS NOT NULL AND \"MaxLongitude\" IS NOT NULL\n    AND \"MinLatitude\" BETWEEN -90 AND 90 AND \"MaxLatitude\" BETWEEN -90 AND 90\n    AND \"MinLongitude\" BETWEEN -180 AND 180 AND \"MaxLongitude\" BETWEEN -180 AND 180\n    AND \"MinLatitude\" <= \"MaxLatitude\" AND \"MinLongitude\" <= \"MaxLongitude\")");
                    table.CheckConstraint("CK_UtilityRoutingRules_IsActive", "\"IsActive\" IN (0, 1)");
                    table.CheckConstraint("CK_UtilityRoutingRules_Name", "length(\"Name\") <= 160 AND length(trim(\"Name\", char(9) || char(10) || char(13) || ' ')) > 0");
                    table.CheckConstraint("CK_UtilityRoutingRules_Priority", "\"Priority\" BETWEEN 0 AND 10000");
                    table.CheckConstraint("CK_UtilityRoutingRules_Utility", "\"Utility\" IN ('Desco', 'Dpdc', 'WaterAuthority', 'GasAuthority')");
                    table.ForeignKey(
                        name: "FK_UtilityRoutingRules_ReportAreas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "ReportAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "UtilityRoutingRules",
                columns: new[] { "Id", "AnomalyType", "AreaId", "IsActive", "MaxLatitude", "MaxLongitude", "MinLatitude", "MinLongitude", "Name", "Priority", "Utility" },
                values: new object[,]
                {
                    { 1, "WaterLeak", null, true, null, null, null, null, "Water leak / generic water authority", 0, "WaterAuthority" },
                    { 2, "GasLeak", null, true, null, null, null, null, "Gas leak / generic gas authority", 0, "GasAuthority" }
                });

            migrationBuilder.InsertData(
                table: "UtilityRoutingRules",
                columns: new[] { "Id", "AnomalyType", "AreaId", "MaxLatitude", "MaxLongitude", "MinLatitude", "MinLongitude", "Name", "Priority", "Utility" },
                values: new object[,]
                {
                    { 10, "ExposedWire", 1, null, null, null, null, "DESCO / Dhanmondi / ExposedWire (unverified template)", 100, "Desco" },
                    { 11, "FallenPole", 1, null, null, null, null, "DESCO / Dhanmondi / FallenPole (unverified template)", 100, "Desco" },
                    { 12, "PowerOutage", 1, null, null, null, null, "DESCO / Dhanmondi / PowerOutage (unverified template)", 100, "Desco" },
                    { 13, "ExposedWire", 2, null, null, null, null, "DESCO / Mirpur / ExposedWire (unverified template)", 100, "Desco" },
                    { 14, "FallenPole", 2, null, null, null, null, "DESCO / Mirpur / FallenPole (unverified template)", 100, "Desco" },
                    { 15, "PowerOutage", 2, null, null, null, null, "DESCO / Mirpur / PowerOutage (unverified template)", 100, "Desco" },
                    { 16, "ExposedWire", 3, null, null, null, null, "DESCO / Gulshan / ExposedWire (unverified template)", 100, "Desco" },
                    { 17, "FallenPole", 3, null, null, null, null, "DESCO / Gulshan / FallenPole (unverified template)", 100, "Desco" },
                    { 18, "PowerOutage", 3, null, null, null, null, "DESCO / Gulshan / PowerOutage (unverified template)", 100, "Desco" },
                    { 19, "ExposedWire", 1, null, null, null, null, "DPDC / Dhanmondi / ExposedWire (unverified template)", 100, "Dpdc" },
                    { 20, "FallenPole", 1, null, null, null, null, "DPDC / Dhanmondi / FallenPole (unverified template)", 100, "Dpdc" },
                    { 21, "PowerOutage", 1, null, null, null, null, "DPDC / Dhanmondi / PowerOutage (unverified template)", 100, "Dpdc" },
                    { 22, "ExposedWire", 2, null, null, null, null, "DPDC / Mirpur / ExposedWire (unverified template)", 100, "Dpdc" },
                    { 23, "FallenPole", 2, null, null, null, null, "DPDC / Mirpur / FallenPole (unverified template)", 100, "Dpdc" },
                    { 24, "PowerOutage", 2, null, null, null, null, "DPDC / Mirpur / PowerOutage (unverified template)", 100, "Dpdc" },
                    { 25, "ExposedWire", 3, null, null, null, null, "DPDC / Gulshan / ExposedWire (unverified template)", 100, "Dpdc" },
                    { 26, "FallenPole", 3, null, null, null, null, "DPDC / Gulshan / FallenPole (unverified template)", 100, "Dpdc" },
                    { 27, "PowerOutage", 3, null, null, null, null, "DPDC / Gulshan / PowerOutage (unverified template)", 100, "Dpdc" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_UtilityRoutingRules_AreaId",
                table: "UtilityRoutingRules",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityRoutingRules_IsActive_AnomalyType_AreaId_Priority",
                table: "UtilityRoutingRules",
                columns: new[] { "IsActive", "AnomalyType", "AreaId", "Priority" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_UtilityRoutingRules_Name",
                table: "UtilityRoutingRules",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UtilityRoutingRules");
        }
    }
}
