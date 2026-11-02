using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCivicConcerns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CivicCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ResidentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Subtype = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1500, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    ObservedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReportedDecibels = table.Column<double>(type: "REAL", nullable: true),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SubmissionToken = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Fingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CivicCases", x => x.Id);
                    table.CheckConstraint("CK_CivicCases_Values", "Kind IN ('Noise','Construction') AND State IN ('Pending','UnderReview','Resolved','Rejected') AND Revision > 0 AND Latitude BETWEEN 23.65 AND 23.95 AND Longitude BETWEEN 90.30 AND 90.55 AND (ReportedDecibels IS NULL OR (Kind = 'Noise' AND ReportedDecibels BETWEEN 0 AND 180))");
                    table.ForeignKey(
                        name: "FK_CivicCases_Users_ResidentId",
                        column: x => x.ResidentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CivicCaseReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    OfficialId = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CivicCaseReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CivicCaseReviews_CivicCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "CivicCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CivicCaseReviews_Users_OfficialId",
                        column: x => x.OfficialId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CivicCaseReviews_CaseId_AtUtc",
                table: "CivicCaseReviews",
                columns: new[] { "CaseId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CivicCaseReviews_OfficialId",
                table: "CivicCaseReviews",
                column: "OfficialId");

            migrationBuilder.CreateIndex(
                name: "IX_CivicCases_Kind_State_CreatedAtUtc",
                table: "CivicCases",
                columns: new[] { "Kind", "State", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CivicCases_ResidentId_CreatedAtUtc",
                table: "CivicCases",
                columns: new[] { "ResidentId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CivicCases_ResidentId_Kind_SubmissionToken",
                table: "CivicCases",
                columns: new[] { "ResidentId", "Kind", "SubmissionToken" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CivicCaseReviews");

            migrationBuilder.DropTable(
                name: "CivicCases");
        }
    }
}
