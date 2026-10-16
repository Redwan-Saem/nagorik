using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReportStatusHistoryActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChangedByUserId",
                table: "ReportStatusHistories",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousStatus",
                table: "ReportStatusHistories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportStatusHistories_ChangedByUserId",
                table: "ReportStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportStatusHistories_Users_ChangedByUserId",
                table: "ReportStatusHistories",
                column: "ChangedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReportStatusHistories_Users_ChangedByUserId",
                table: "ReportStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_ReportStatusHistories_ChangedByUserId",
                table: "ReportStatusHistories");

            migrationBuilder.DropColumn(
                name: "ChangedByUserId",
                table: "ReportStatusHistories");

            migrationBuilder.DropColumn(
                name: "PreviousStatus",
                table: "ReportStatusHistories");
        }
    }
}
