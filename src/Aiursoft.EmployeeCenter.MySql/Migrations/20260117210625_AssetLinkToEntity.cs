using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AssetLinkToEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyEntityId",
                table: "Assets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_CompanyEntityId",
                table: "Assets",
                column: "CompanyEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_CompanyEntities_CompanyEntityId",
                table: "Assets",
                column: "CompanyEntityId",
                principalTable: "CompanyEntities",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_CompanyEntities_CompanyEntityId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_CompanyEntityId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CompanyEntityId",
                table: "Assets");
        }
    }
}
