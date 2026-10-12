using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
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
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MeetingMinutesAttemptCount",
                table: "AudioAsrResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MeetingMinutesMarkdown",
                table: "AudioAsrResults",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
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
