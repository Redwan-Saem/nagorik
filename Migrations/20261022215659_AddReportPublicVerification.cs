using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReportPublicVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPubliclyVerified",
                table: "Reports",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "PublicVerificationRevision",
                table: "Reports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublicVerifiedAtUtc",
                table: "Reports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PublicVerifiedByUserId",
                table: "Reports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_IsPubliclyVerified_CreatedAt",
                table: "Reports",
                columns: new[] { "IsPubliclyVerified", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_PublicVerifiedByUserId",
                table: "Reports",
                column: "PublicVerifiedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reports_PublicVerification",
                table: "Reports",
                sql: "\"PublicVerificationRevision\" >= 0 AND (\"IsPubliclyVerified\" = 0 OR (\"PublicVerifiedByUserId\" IS NOT NULL AND \"PublicVerifiedAtUtc\" IS NOT NULL))");

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Users_PublicVerifiedByUserId",
                table: "Reports",
                column: "PublicVerifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Users_PublicVerifiedByUserId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_IsPubliclyVerified_CreatedAt",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_PublicVerifiedByUserId",
                table: "Reports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reports_PublicVerification",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "IsPubliclyVerified",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "PublicVerificationRevision",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "PublicVerifiedAtUtc",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "PublicVerifiedByUserId",
                table: "Reports");
        }
    }
}
