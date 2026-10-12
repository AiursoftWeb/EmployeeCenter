using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddDualStackAndFrpsServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FrpsServerId",
                table: "Services",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ipv6Address",
                table: "Servers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Services_FrpsServerId",
                table: "Services",
                column: "FrpsServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Services_Servers_FrpsServerId",
                table: "Services",
                column: "FrpsServerId",
                principalTable: "Servers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Services_Servers_FrpsServerId",
                table: "Services");

            migrationBuilder.DropIndex(
                name: "IX_Services_FrpsServerId",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "FrpsServerId",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "Ipv6Address",
                table: "Servers");
        }
    }
}
