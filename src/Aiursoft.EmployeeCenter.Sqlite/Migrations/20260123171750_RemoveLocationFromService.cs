using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLocationFromService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Services_Locations_LocationId",
                table: "Services");

            migrationBuilder.DropIndex(
                name: "IX_Services_LocationId",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Services");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "Services",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Services_LocationId",
                table: "Services",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Services_Locations_LocationId",
                table: "Services",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id");
        }
    }
}
