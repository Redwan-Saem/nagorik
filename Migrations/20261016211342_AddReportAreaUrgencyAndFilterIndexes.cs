using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReportAreaUrgencyAndFilterIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AreaId",
                table: "Reports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Urgency",
                table: "Reports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "ReportAreas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportAreas", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ReportAreas",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Dhanmondi" },
                    { 2, "Mirpur" },
                    { 3, "Gulshan" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_AreaId_Category_CreatedAt_Id",
                table: "Reports",
                columns: new[] { "AreaId", "Category", "CreatedAt", "Id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_AreaId_CreatedAt_Id",
                table: "Reports",
                columns: new[] { "AreaId", "CreatedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Category_CreatedAt_Id",
                table: "Reports",
                columns: new[] { "Category", "CreatedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_CreatedAt_Id",
                table: "Reports",
                columns: new[] { "CreatedAt", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Status_CreatedAt_Id",
                table: "Reports",
                columns: new[] { "Status", "CreatedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Urgency_CreatedAt_Id",
                table: "Reports",
                columns: new[] { "Urgency", "CreatedAt", "Id" },
                descending: new bool[0]);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reports_Urgency",
                table: "Reports",
                sql: "\"Urgency\" IN (0, 1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_ReportAreas_Name",
                table: "ReportAreas",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_ReportAreas_AreaId",
                table: "Reports",
                column: "AreaId",
                principalTable: "ReportAreas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reports_ReportAreas_AreaId",
                table: "Reports");

            migrationBuilder.DropTable(
                name: "ReportAreas");

            migrationBuilder.DropIndex(
                name: "IX_Reports_AreaId_Category_CreatedAt_Id",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_AreaId_CreatedAt_Id",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_Category_CreatedAt_Id",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_CreatedAt_Id",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_Status_CreatedAt_Id",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_Urgency_CreatedAt_Id",
                table: "Reports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reports_Urgency",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "AreaId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "Urgency",
                table: "Reports");
        }
    }
}
