using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeContractOcrResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContractOcrResults_ContractId",
                table: "ContractOcrResults");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastOcrAttemptTime",
                table: "Contracts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OcrAttemptCount",
                table: "Contracts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PlainText",
                table: "ContractOcrResults",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractOcrResults_ContractId",
                table: "ContractOcrResults",
                column: "ContractId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContractOcrResults_ContractId",
                table: "ContractOcrResults");

            migrationBuilder.DropColumn(
                name: "LastOcrAttemptTime",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "OcrAttemptCount",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "PlainText",
                table: "ContractOcrResults");

            migrationBuilder.CreateIndex(
                name: "IX_ContractOcrResults_ContractId",
                table: "ContractOcrResults",
                column: "ContractId");
        }
    }
}
