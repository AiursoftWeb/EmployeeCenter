using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioMediaRecoveryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Audios_MediaStatus_CreateTime",
                table: "Audios",
                columns: new[] { "MediaStatus", "CreateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Audios_MediaStatus_MediaProcessingStartedTime",
                table: "Audios",
                columns: new[] { "MediaStatus", "MediaProcessingStartedTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Audios_MediaStatus_CreateTime",
                table: "Audios");

            migrationBuilder.DropIndex(
                name: "IX_Audios_MediaStatus_MediaProcessingStartedTime",
                table: "Audios");
        }
    }
}
