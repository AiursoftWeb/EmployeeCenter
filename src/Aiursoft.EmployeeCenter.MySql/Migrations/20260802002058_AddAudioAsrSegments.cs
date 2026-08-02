using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
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
                    AudioId = table.Column<int>(type: "int", nullable: false),
                    SegmentIndex = table.Column<int>(type: "int", nullable: false),
                    CoreStartMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    CoreEndMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    InputStartMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    InputEndMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    SegmentDurationSeconds = table.Column<int>(type: "int", nullable: false),
                    OverlapSeconds = table.Column<int>(type: "int", nullable: false),
                    SegmentsJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PlainText = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudioAsrSegments");
        }
    }
}
