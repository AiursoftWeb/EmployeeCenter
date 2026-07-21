using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
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
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Audios",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ViewScope",
                table: "Audios",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AudioShares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AudioId = table.Column<int>(type: "INTEGER", nullable: false),
                    SharedWithUserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SharedWithRoleId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    Permission = table.Column<int>(type: "INTEGER", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                });

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
