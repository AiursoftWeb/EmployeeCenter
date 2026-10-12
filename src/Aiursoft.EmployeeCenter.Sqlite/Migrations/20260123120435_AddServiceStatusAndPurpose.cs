using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceStatusAndPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsOnline",
                table: "Services",
                newName: "Status");

            migrationBuilder.AddColumn<bool>(
                name: "AuthentikIntegrated",
                table: "Services",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "Services",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthentikIntegrated",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "Services");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Services",
                newName: "IsOnline");
        }
    }
}
