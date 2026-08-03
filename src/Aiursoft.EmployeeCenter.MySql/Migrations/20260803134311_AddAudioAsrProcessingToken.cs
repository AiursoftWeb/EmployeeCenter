using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioAsrProcessingToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AsrProcessingToken",
                table: "Audios",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AsrProcessingToken",
                table: "Audios");
        }
    }
}
