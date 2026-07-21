using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioSharingAndUserDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerDepartment",
                table: "Audios",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Audios",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ViewScope",
                table: "Audios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "AspNetUsers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AudioShares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AudioId = table.Column<int>(type: "int", nullable: false),
                    SharedWithUserId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SharedWithRoleId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Permission = table.Column<int>(type: "int", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioShares_AspNetUsers_SharedWithUserId",
                        column: x => x.SharedWithUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AudioShares_Audios_AudioId",
                        column: x => x.AudioId,
                        principalTable: "Audios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Audios_OwnerId",
                table: "Audios",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioShares_AudioId",
                table: "AudioShares",
                column: "AudioId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioShares_SharedWithUserId",
                table: "AudioShares",
                column: "SharedWithUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Audios_AspNetUsers_OwnerId",
                table: "Audios",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Audios_AspNetUsers_OwnerId",
                table: "Audios");

            migrationBuilder.DropTable(
                name: "AudioShares");

            migrationBuilder.DropIndex(
                name: "IX_Audios_OwnerId",
                table: "Audios");

            migrationBuilder.DropColumn(
                name: "OwnerDepartment",
                table: "Audios");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Audios");

            migrationBuilder.DropColumn(
                name: "ViewScope",
                table: "Audios");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "AspNetUsers");
        }
    }
}
