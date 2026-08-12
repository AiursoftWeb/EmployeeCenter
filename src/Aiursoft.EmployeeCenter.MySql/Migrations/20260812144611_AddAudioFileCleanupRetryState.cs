using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioFileCleanupRetryState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AudioFileDeletions_CreatedTime",
                table: "AudioFileDeletions");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "AudioFileDeletions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeadLetter",
                table: "AudioFileDeletions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "AudioFileDeletions",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptTime",
                table: "AudioFileDeletions",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_AudioFileDeletions_IsDeadLetter_NextAttemptTime",
                table: "AudioFileDeletions",
                columns: new[] { "IsDeadLetter", "NextAttemptTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AudioFileDeletions_IsDeadLetter_NextAttemptTime",
                table: "AudioFileDeletions");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "AudioFileDeletions");

            migrationBuilder.DropColumn(
                name: "IsDeadLetter",
                table: "AudioFileDeletions");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "AudioFileDeletions");

            migrationBuilder.DropColumn(
                name: "NextAttemptTime",
                table: "AudioFileDeletions");

            migrationBuilder.CreateIndex(
                name: "IX_AudioFileDeletions_CreatedTime",
                table: "AudioFileDeletions",
                column: "CreatedTime");
        }
    }
}
