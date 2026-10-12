using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PromotionHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    OldJobLevel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    NewJobLevel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    OldTitle = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    NewTitle = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ChangeTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PromoterId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromotionHistories_AspNetUsers_PromoterId",
                        column: x => x.PromoterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PromotionHistories_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromotionHistories_PromoterId",
                table: "PromotionHistories",
                column: "PromoterId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionHistories_UserId",
                table: "PromotionHistories",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromotionHistories");
        }
    }
}
