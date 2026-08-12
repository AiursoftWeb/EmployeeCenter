using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastMeetingMinutesAttemptTime",
                table: "AudioAsrResults",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MeetingMinutesAttemptCount",
                table: "AudioAsrResults",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MeetingMinutesMarkdown",
                table: "AudioAsrResults",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastMeetingMinutesAttemptTime",
                table: "AudioAsrResults");

            migrationBuilder.DropColumn(
                name: "MeetingMinutesAttemptCount",
                table: "AudioAsrResults");

            migrationBuilder.DropColumn(
                name: "MeetingMinutesMarkdown",
                table: "AudioAsrResults");
        }
    }
}
