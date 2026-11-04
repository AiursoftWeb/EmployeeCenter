using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class ContractChangeHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HistoryKey",
                table: "Contracts",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE Contracts SET HistoryKey = lower(hex(randomblob(16)))");

            migrationBuilder.CreateTable(
                name: "ContractChangeLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContractKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ContractName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    BeforeJson = table.Column<string>(type: "TEXT", nullable: true),
                    AfterJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractChangeLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractChangeLogs_ContractKey_CreatedAt",
                table: "ContractChangeLogs",
                columns: new[] { "ContractKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractChangeLogs_CreatedAt",
                table: "ContractChangeLogs",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractChangeLogs");

            migrationBuilder.DropColumn(
                name: "HistoryKey",
                table: "Contracts");
        }
    }
}
