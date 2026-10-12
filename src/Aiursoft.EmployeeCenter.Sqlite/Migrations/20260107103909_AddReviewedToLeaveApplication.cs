using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewedToLeaveApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewedById",
                table: "LeaveApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveApplications_ReviewedById",
                table: "LeaveApplications",
                column: "ReviewedById");

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveApplications_AspNetUsers_ReviewedById",
                table: "LeaveApplications",
                column: "ReviewedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeaveApplications_AspNetUsers_ReviewedById",
                table: "LeaveApplications");

            migrationBuilder.DropIndex(
                name: "IX_LeaveApplications_ReviewedById",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "ReviewedById",
                table: "LeaveApplications");
        }
    }
}
