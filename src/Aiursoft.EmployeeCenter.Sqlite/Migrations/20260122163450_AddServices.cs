using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DnsProviders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DnsProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Domain = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: true),
                    CrossEntityLinkId = table.Column<int>(type: "INTEGER", nullable: true),
                    Protocols = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LocationId = table.Column<int>(type: "INTEGER", nullable: true),
                    ServerIp = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DnsProviderId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsViaFrps = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCloudflareProxied = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSelfDeveloped = table.Column<bool>(type: "INTEGER", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Services_CompanyEntities_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "CompanyEntities",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Services_DnsProviders_DnsProviderId",
                        column: x => x.DnsProviderId,
                        principalTable: "DnsProviders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Services_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Services_Services_CrossEntityLinkId",
                        column: x => x.CrossEntityLinkId,
                        principalTable: "Services",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Services_CrossEntityLinkId",
                table: "Services",
                column: "CrossEntityLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_DnsProviderId",
                table: "Services",
                column: "DnsProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_LocationId",
                table: "Services",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_OwnerId",
                table: "Services",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "DnsProviders");
        }
    }
}
