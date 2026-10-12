using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyEntities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CompanyNameEnglish = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EntityCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CINumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RegisteredAddress = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    OfficeAddress = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LegalRepresentative = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CompanyType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EstablishmentDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BankName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BankAccount = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BankAccountName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    LogoPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SealPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LicensePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RegisteredCapital = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    OperationStatus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    SCRLocation = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CompanySecretary = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyEntities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyEntityLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyEntityId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LogTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    Snapshot = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyEntityLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyEntityLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyEntityLogs_CompanyEntities_CompanyEntityId",
                        column: x => x.CompanyEntityId,
                        principalTable: "CompanyEntities",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEntityLogs_CompanyEntityId",
                table: "CompanyEntityLogs",
                column: "CompanyEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEntityLogs_UserId",
                table: "CompanyEntityLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyEntityLogs");

            migrationBuilder.DropTable(
                name: "CompanyEntities");
        }
    }
}
