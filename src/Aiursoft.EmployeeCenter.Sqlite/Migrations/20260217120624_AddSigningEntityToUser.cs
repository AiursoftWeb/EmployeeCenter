using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSigningEntityToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SigningEntityId",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SigningEntityId",
                table: "AspNetUsers",
                column: "SigningEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_CompanyEntities_SigningEntityId",
                table: "AspNetUsers",
                column: "SigningEntityId",
                principalTable: "CompanyEntities",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_CompanyEntities_SigningEntityId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_SigningEntityId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SigningEntityId",
                table: "AspNetUsers");
        }
    }
}
