using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDrainageWorkReportLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastWorkReportId",
                table: "DrainagePoints",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrainagePoints_LastWorkReportId",
                table: "DrainagePoints",
                column: "LastWorkReportId");

            migrationBuilder.AddForeignKey(
                name: "FK_DrainagePoints_Reports_LastWorkReportId",
                table: "DrainagePoints",
                column: "LastWorkReportId",
                principalTable: "Reports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DrainagePoints_Reports_LastWorkReportId",
                table: "DrainagePoints");

            migrationBuilder.DropIndex(
                name: "IX_DrainagePoints_LastWorkReportId",
                table: "DrainagePoints");

            migrationBuilder.DropColumn(
                name: "LastWorkReportId",
                table: "DrainagePoints");
        }
    }
}
