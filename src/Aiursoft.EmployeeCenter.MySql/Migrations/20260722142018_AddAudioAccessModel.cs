using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioAccessModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Department column only if it does not already exist.
            // (The deleted 20260721170640_AddAudioSharingAndUserDepartment may have
            //  already added this column on databases that deployed that migration.)
            migrationBuilder.Sql(@"SET @col_exists = (
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                AND TABLE_NAME = 'AspNetUsers'
                AND COLUMN_NAME = 'Department'
            );
            SET @stmt = IF(@col_exists = 0,
                'ALTER TABLE `AspNetUsers` ADD `Department` varchar(100) CHARACTER SET utf8mb4 NULL',
                'SELECT 1');
            PREPARE stmt FROM @stmt;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;");

            migrationBuilder.CreateTable(
                name: "Audios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FilePath = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AsrAttemptCount = table.Column<int>(type: "int", nullable: false),
                    EmptyResultCount = table.Column<int>(type: "int", nullable: false),
                    LastAsrAttemptTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    OwnerId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AudienceDepartment = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ViewScope = table.Column<int>(type: "int", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AudioAsrResults",
                columns: table => new
                {
                    AudioId = table.Column<int>(type: "int", nullable: false),
                    PlainText = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AudioShares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AudioId = table.Column<int>(type: "int", nullable: false),
                    SharedWithUserId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SharedWithRoleId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Permission = table.Column<int>(type: "int", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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

            // Drop Department column only if it still exists.
            migrationBuilder.Sql(@"SET @col_exists = (
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                AND TABLE_NAME = 'AspNetUsers'
                AND COLUMN_NAME = 'Department'
            );
            SET @stmt = IF(@col_exists > 0,
                'ALTER TABLE `AspNetUsers` DROP COLUMN `Department`',
                'SELECT 1');
            PREPARE stmt FROM @stmt;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;");
        }
    }
}
