using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioAsrSegments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudioAsrSegments");
        }
    }
}
