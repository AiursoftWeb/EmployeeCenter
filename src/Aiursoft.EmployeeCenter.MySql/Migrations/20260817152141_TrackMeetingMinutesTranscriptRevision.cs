using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
{
    /// <inheritdoc />
    public partial class TrackMeetingMinutesTranscriptRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MeetingMinutesTranscriptRevision",
                table: "AudioAsrResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TranscriptRevision",
                table: "AudioAsrResults",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MeetingMinutesTranscriptRevision",
                table: "AudioAsrResults");

            migrationBuilder.DropColumn(
                name: "TranscriptRevision",
                table: "AudioAsrResults");
        }
    }
}
