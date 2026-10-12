using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMoreIntangibleAssetsAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyEntityId",
                table: "IntangibleAssets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "IntangibleAssets",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "IntangibleAssets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationCertificateUrl",
                table: "IntangibleAssets",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntangibleAssets_CompanyEntityId",
                table: "IntangibleAssets",
                column: "CompanyEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_IntangibleAssets_CompanyEntities_CompanyEntityId",
                table: "IntangibleAssets",
                column: "CompanyEntityId",
                principalTable: "CompanyEntities",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IntangibleAssets_CompanyEntities_CompanyEntityId",
                table: "IntangibleAssets");

            migrationBuilder.DropIndex(
                name: "IX_IntangibleAssets_CompanyEntityId",
                table: "IntangibleAssets");

            migrationBuilder.DropColumn(
                name: "CompanyEntityId",
                table: "IntangibleAssets");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "IntangibleAssets");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "IntangibleAssets");

            migrationBuilder.DropColumn(
                name: "RegistrationCertificateUrl",
                table: "IntangibleAssets");
        }
    }
}
