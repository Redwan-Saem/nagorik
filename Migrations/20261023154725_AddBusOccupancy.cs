using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBusOccupancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusOccupancyReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ResidentId = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkId = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ObservedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusOccupancyReports", x => x.Id);
                    table.CheckConstraint("CK_BusOccupancy_Level", "Level IN ('SeatsAvailable','Standing','Full')");
                    table.ForeignKey(
                        name: "FK_BusOccupancyReports_TransportLinks_LinkId",
                        column: x => x.LinkId,
                        principalTable: "TransportLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusOccupancyReports_Users_ResidentId",
                        column: x => x.ResidentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusOccupancyReports_LinkId_ObservedAtUtc",
                table: "BusOccupancyReports",
                columns: new[] { "LinkId", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BusOccupancyReports_ResidentId_LinkId",
                table: "BusOccupancyReports",
                columns: new[] { "ResidentId", "LinkId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusOccupancyReports");
        }
    }
}
