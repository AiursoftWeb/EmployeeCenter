using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyEntityToServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyEntityId",
                table: "Servers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Servers_CompanyEntityId",
                table: "Servers",
                column: "CompanyEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Servers_CompanyEntities_CompanyEntityId",
                table: "Servers",
                column: "CompanyEntityId",
                principalTable: "CompanyEntities",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Servers_CompanyEntities_CompanyEntityId",
                table: "Servers");

            migrationBuilder.DropIndex(
                name: "IX_Servers_CompanyEntityId",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "CompanyEntityId",
                table: "Servers");
        }
    }
}
