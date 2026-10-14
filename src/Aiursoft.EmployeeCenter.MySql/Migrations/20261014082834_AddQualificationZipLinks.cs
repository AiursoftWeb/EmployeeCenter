using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
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
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ArchiveSha256",
                table: "QualificationDownloads",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLinkIssuedAt",
                table: "QualificationDownloads",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkIssuedCount",
                table: "QualificationDownloads",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "QualificationLinkIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    QualificationDownloadId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IssuedToId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssuedToName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssuedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualificationLinkIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualificationLinkIssues_QualificationDownloads_Qualification~",
                        column: x => x.QualificationDownloadId,
                        principalTable: "QualificationDownloads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
