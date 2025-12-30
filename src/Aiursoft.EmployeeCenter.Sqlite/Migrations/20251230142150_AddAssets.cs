using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhysicalAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    TotalStock = table.Column<int>(type: "INTEGER", nullable: false),
                    FrozenStock = table.Column<int>(type: "INTEGER", nullable: false),
                    UsedStock = table.Column<int>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VirtualAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LoginUrl = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EncryptedPassword = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    EncryptedTotpSecret = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsHighRisk = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalAssetUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedSerialNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ApplyTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReturnTime = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalAssetUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysicalAssetUsages_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhysicalAssetUsages_PhysicalAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "PhysicalAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalAssetUsages_AssetId",
                table: "PhysicalAssetUsages",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalAssetUsages_UserId",
                table: "PhysicalAssetUsages",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhysicalAssetUsages");

            migrationBuilder.DropTable(
                name: "VirtualAssets");

            migrationBuilder.DropTable(
                name: "PhysicalAssets");
        }
    }
}
