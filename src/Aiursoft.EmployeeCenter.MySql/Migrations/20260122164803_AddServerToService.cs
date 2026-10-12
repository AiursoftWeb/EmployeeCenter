using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddServerToService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ServerId",
                table: "Services",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Services_ServerId",
                table: "Services",
                column: "ServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Services_Assets_ServerId",
                table: "Services",
                column: "ServerId",
                principalTable: "Assets",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Services_Assets_ServerId",
                table: "Services");

            migrationBuilder.DropIndex(
                name: "IX_Services_ServerId",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "Services");
        }
    }
}
