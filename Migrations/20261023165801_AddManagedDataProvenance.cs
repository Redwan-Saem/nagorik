using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedDataProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BoundaryIsSample",
                table: "Zones",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoundarySource",
                table: "Zones",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GeoRevision",
                table: "Zones",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "GeoUpdatedAtUtc",
                table: "Zones",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InputIsSample",
                table: "Zones",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InputSource",
                table: "Zones",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ManagedDataChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ZoneId = table.Column<int>(type: "INTEGER", nullable: true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ActorUserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    IsSample = table.Column<bool>(type: "INTEGER", nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagedDataChanges", x => x.Id);
                    table.CheckConstraint("CK_ManagedDataChange_Json", "json_valid(PayloadJson)");
                    table.ForeignKey(
                        name: "FK_ManagedDataChanges_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransportCatalogStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransportCatalogStates", x => x.Id);
                    table.CheckConstraint("CK_TransportCatalogState_Singleton", "Id = 1 AND Revision > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManagedDataChanges_ZoneId_AtUtc",
                table: "ManagedDataChanges",
                columns: new[] { "ZoneId", "AtUtc" });
            migrationBuilder.Sql("UPDATE Zones SET BoundaryIsSample = 1, BoundarySource = 'Existing explicitly labelled sample boundary; original source unrecorded' WHERE Name LIKE '%(sample boundary)%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManagedDataChanges");

            migrationBuilder.DropTable(
                name: "TransportCatalogStates");

            migrationBuilder.DropColumn(
                name: "BoundaryIsSample",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "BoundarySource",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "GeoRevision",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "GeoUpdatedAtUtc",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "InputIsSample",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "InputSource",
                table: "Zones");
        }
    }
}
