using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
{
    /// <inheritdoc />
    public partial class SecureAudioUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AsrTerminalError",
                table: "Audios",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MediaProcessingError",
                table: "Audios",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "MediaProcessingStartedTime",
                table: "Audios",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaProcessingToken",
                table: "Audios",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "MediaStatus",
                table: "Audios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PendingFilePath",
                table: "Audios",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AudioUploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OwnerId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FilePath = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    TargetAudioId = table.Column<int>(type: "int", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ConsumedTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioUploads", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Audios_FilePath",
                table: "Audios",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Audios_PendingFilePath",
                table: "Audios",
                column: "PendingFilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AudioUploads_FilePath",
                table: "AudioUploads",
                column: "FilePath",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudioUploads");

            migrationBuilder.DropIndex(
                name: "IX_Audios_FilePath",
                table: "Audios");

            migrationBuilder.DropIndex(
                name: "IX_Audios_PendingFilePath",
                table: "Audios");

            migrationBuilder.DropColumn(
                name: "AsrTerminalError",
                table: "Audios");

            migrationBuilder.DropColumn(
                name: "MediaProcessingError",
                table: "Audios");

            migrationBuilder.DropColumn(
                name: "MediaProcessingStartedTime",
                table: "Audios");

            migrationBuilder.DropColumn(
                name: "MediaProcessingToken",
                table: "Audios");

            migrationBuilder.DropColumn(
                name: "MediaStatus",
                table: "Audios");

            migrationBuilder.DropColumn(
                name: "PendingFilePath",
                table: "Audios");
        }
    }
}
