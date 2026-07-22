using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioAccessModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Audios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AsrAttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    EmptyResultCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastAsrAttemptTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    AudienceDepartment = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ViewScope = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Audios_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AudioAsrResults",
                columns: table => new
                {
                    AudioId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlainText = table.Column<string>(type: "TEXT", nullable: false),
                    CreateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioAsrResults", x => x.AudioId);
                    table.ForeignKey(
                        name: "FK_AudioAsrResults_Audios_AudioId",
                        column: x => x.AudioId,
                        principalTable: "Audios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AudioShares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AudioId = table.Column<int>(type: "INTEGER", nullable: false),
                    SharedWithUserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SharedWithRoleId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    Permission = table.Column<int>(type: "INTEGER", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioShares", x => x.Id);
                    table.CheckConstraint("CK_AudioShares_ExactlyOneRecipient", "(SharedWithUserId IS NOT NULL AND SharedWithRoleId IS NULL) OR (SharedWithUserId IS NULL AND SharedWithRoleId IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AudioShares_AspNetRoles_SharedWithRoleId",
                        column: x => x.SharedWithRoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AudioShares_AspNetUsers_SharedWithUserId",
                        column: x => x.SharedWithUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AudioShares_Audios_AudioId",
                        column: x => x.AudioId,
                        principalTable: "Audios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Audios_OwnerId",
                table: "Audios",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioShares_AudioId_SharedWithRoleId",
                table: "AudioShares",
                columns: new[] { "AudioId", "SharedWithRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AudioShares_AudioId_SharedWithUserId",
                table: "AudioShares",
                columns: new[] { "AudioId", "SharedWithUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AudioShares_SharedWithRoleId",
                table: "AudioShares",
                column: "SharedWithRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioShares_SharedWithUserId",
                table: "AudioShares",
                column: "SharedWithUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudioAsrResults");

            migrationBuilder.DropTable(
                name: "AudioShares");

            migrationBuilder.DropTable(
                name: "Audios");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "AspNetUsers");
        }
    }
}
