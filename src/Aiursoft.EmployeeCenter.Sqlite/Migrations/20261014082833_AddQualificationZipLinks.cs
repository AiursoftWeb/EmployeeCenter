using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddQualificationZipLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchivePath",
                table: "QualificationDownloads",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveSha256",
                table: "QualificationDownloads",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLinkIssuedAt",
                table: "QualificationDownloads",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkIssuedCount",
                table: "QualificationDownloads",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "QualificationLinkIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    QualificationDownloadId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IssuedToId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    IssuedToName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualificationLinkIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualificationLinkIssues_QualificationDownloads_QualificationDownloadId",
                        column: x => x.QualificationDownloadId,
                        principalTable: "QualificationDownloads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QualificationLinkIssues_IssuedToId_IssuedAt",
                table: "QualificationLinkIssues",
                columns: new[] { "IssuedToId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QualificationLinkIssues_QualificationDownloadId_IssuedAt",
                table: "QualificationLinkIssues",
                columns: new[] { "QualificationDownloadId", "IssuedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QualificationLinkIssues");

            migrationBuilder.DropColumn(
                name: "ArchivePath",
                table: "QualificationDownloads");

            migrationBuilder.DropColumn(
                name: "ArchiveSha256",
                table: "QualificationDownloads");

            migrationBuilder.DropColumn(
                name: "LastLinkIssuedAt",
                table: "QualificationDownloads");

            migrationBuilder.DropColumn(
                name: "LinkIssuedCount",
                table: "QualificationDownloads");
        }
    }
}
