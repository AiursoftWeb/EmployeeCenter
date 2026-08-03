using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioAsrActiveTaskId : Migration
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AsrActiveTaskId",
                table: "Audios");
        }
    }
}
