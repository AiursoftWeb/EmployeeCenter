using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddContractFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FolderId",
                table: "Contracts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContractFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParentFolderId = table.Column<int>(type: "int", nullable: true),
                    CreateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractFolders_ContractFolders_ParentFolderId",
                        column: x => x.ParentFolderId,
                        principalTable: "ContractFolders",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_FolderId",
                table: "Contracts",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractFolders_ParentFolderId_Name",
                table: "ContractFolders",
                columns: new[] { "ParentFolderId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_ContractFolders_FolderId",
                table: "Contracts",
                column: "FolderId",
                principalTable: "ContractFolders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_ContractFolders_FolderId",
                table: "Contracts");

            migrationBuilder.DropTable(
                name: "ContractFolders");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_FolderId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "Contracts");
        }
    }
}
