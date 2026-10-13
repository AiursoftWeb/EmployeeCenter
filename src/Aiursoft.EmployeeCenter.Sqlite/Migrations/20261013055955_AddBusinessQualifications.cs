using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessQualifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessQualifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyEntityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    OriginalPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OriginalSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedById = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessQualifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessQualifications_CompanyEntities_CompanyEntityId",
                        column: x => x.CompanyEntityId,
                        principalTable: "CompanyEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualificationDownloads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BusinessQualificationId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicantId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ApplicantName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    QualificationName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    OriginalSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Recipient = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GeneratedPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    GeneratedSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastDownloadedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DownloadCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualificationDownloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualificationDownloads_BusinessQualifications_BusinessQualificationId",
                        column: x => x.BusinessQualificationId,
                        principalTable: "BusinessQualifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessQualifications_CompanyEntityId",
                table: "BusinessQualifications",
                column: "CompanyEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessQualifications_IsActive_CreatedAt",
                table: "BusinessQualifications",
                columns: new[] { "IsActive", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QualificationDownloads_ApplicantId_CreatedAt",
                table: "QualificationDownloads",
                columns: new[] { "ApplicantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QualificationDownloads_BusinessQualificationId",
                table: "QualificationDownloads",
                column: "BusinessQualificationId");

            migrationBuilder.CreateIndex(
                name: "IX_QualificationDownloads_CreatedAt",
                table: "QualificationDownloads",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QualificationDownloads");

            migrationBuilder.DropTable(
                name: "BusinessQualifications");
        }
    }
}
