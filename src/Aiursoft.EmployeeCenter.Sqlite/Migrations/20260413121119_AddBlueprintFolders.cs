using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddBlueprintFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FolderId",
                table: "Blueprints",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BlueprintFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ParentFolderId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlueprintFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlueprintFolders_BlueprintFolders_ParentFolderId",
                        column: x => x.ParentFolderId,
                        principalTable: "BlueprintFolders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Blueprints_FolderId",
                table: "Blueprints",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_BlueprintFolders_ParentFolderId_Name",
                table: "BlueprintFolders",
                columns: new[] { "ParentFolderId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Blueprints_BlueprintFolders_FolderId",
                table: "Blueprints",
                column: "FolderId",
                principalTable: "BlueprintFolders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Blueprints_BlueprintFolders_FolderId",
                table: "Blueprints");

            migrationBuilder.DropTable(
                name: "BlueprintFolders");

            migrationBuilder.DropIndex(
                name: "IX_Blueprints_FolderId",
                table: "Blueprints");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "Blueprints");
        }
    }
}
