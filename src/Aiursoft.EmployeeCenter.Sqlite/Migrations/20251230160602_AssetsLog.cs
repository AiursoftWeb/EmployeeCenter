using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AssetsLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetEventLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperatorId = table.Column<string>(type: "TEXT", nullable: false),
                    FromStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ToStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    EventTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetEventLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetEventLogs_AspNetUsers_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetEventLogs_PhysicalAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "PhysicalAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VirtualAssetAccessLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    AccessTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MfaVerified = table.Column<bool>(type: "INTEGER", nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualAssetAccessLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VirtualAssetAccessLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VirtualAssetAccessLogs_VirtualAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "VirtualAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetEventLogs_AssetId",
                table: "AssetEventLogs",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetEventLogs_OperatorId",
                table: "AssetEventLogs",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualAssetAccessLogs_AssetId",
                table: "VirtualAssetAccessLogs",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualAssetAccessLogs_UserId",
                table: "VirtualAssetAccessLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetEventLogs");

            migrationBuilder.DropTable(
                name: "VirtualAssetAccessLogs");
        }
    }
}
