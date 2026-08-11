using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSecureAudioProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AsrActiveTaskId",
                table: "Audios",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AsrProcessingToken",
                table: "Audios",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AsrTerminalError",
                table: "Audios",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaProcessingError",
                table: "Audios",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MediaProcessingStartedTime",
                table: "Audios",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaProcessingToken",
                table: "Audios",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaStatus",
                table: "Audios",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PendingFilePath",
                table: "Audios",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AudioAsrSegments",
                columns: table => new
                {
                    AudioId = table.Column<int>(type: "INTEGER", nullable: false),
                    SegmentIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    CoreStartMilliseconds = table.Column<long>(type: "INTEGER", nullable: false),
                    CoreEndMilliseconds = table.Column<long>(type: "INTEGER", nullable: false),
                    InputStartMilliseconds = table.Column<long>(type: "INTEGER", nullable: false),
                    InputEndMilliseconds = table.Column<long>(type: "INTEGER", nullable: false),
                    SegmentDurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    OverlapSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    SegmentsJson = table.Column<string>(type: "TEXT", nullable: false),
                    PlainText = table.Column<string>(type: "TEXT", nullable: false),
                    CreateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioAsrSegments", x => new { x.AudioId, x.SegmentIndex });
                    table.ForeignKey(
                        name: "FK_AudioAsrSegments_Audios_AudioId",
                        column: x => x.AudioId,
                        principalTable: "Audios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AudioUploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Purpose = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetAudioId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConsumedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioUploads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Audios_FilePath",
                table: "Audios",
                column: "FilePath");

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
                name: "AudioAsrSegments");

            migrationBuilder.DropTable(
                name: "AudioUploads");

            migrationBuilder.DropIndex(
                name: "IX_Audios_FilePath",
                table: "Audios");

            migrationBuilder.DropIndex(
                name: "IX_Audios_PendingFilePath",
                table: "Audios");

            migrationBuilder.DropColumn(
                name: "AsrActiveTaskId",
                table: "Audios");

            migrationBuilder.DropColumn(
                name: "AsrProcessingToken",
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
