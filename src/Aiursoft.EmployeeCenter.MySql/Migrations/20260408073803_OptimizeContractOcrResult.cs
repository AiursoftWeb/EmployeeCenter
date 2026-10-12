using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeContractOcrResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractOcrResults_Contracts_ContractId",
                table: "ContractOcrResults");

            migrationBuilder.DropIndex(
                name: "IX_ContractOcrResults_ContractId",
                table: "ContractOcrResults");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastOcrAttemptTime",
                table: "Contracts",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OcrAttemptCount",
                table: "Contracts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PlainText",
                table: "ContractOcrResults",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ContractOcrResults_ContractId",
                table: "ContractOcrResults",
                column: "ContractId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ContractOcrResults_Contracts_ContractId",
                table: "ContractOcrResults",
                column: "ContractId",
                principalTable: "Contracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractOcrResults_Contracts_ContractId",
                table: "ContractOcrResults");

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

            migrationBuilder.AddForeignKey(
                name: "FK_ContractOcrResults_Contracts_ContractId",
                table: "ContractOcrResults",
                column: "ContractId",
                principalTable: "Contracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
