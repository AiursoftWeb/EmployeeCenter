using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
{
    /// <inheritdoc />
    public partial class InfrastructureRegistryV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyToken",
                table: "Services",
                type: "varchar(36)",
                maxLength: 36,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsRegistryValidated",
                table: "Services",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Services",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPrimaryDomain",
                table: "Services",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "RetiredAt",
                table: "Services",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetiredByUserId",
                table: "Services",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyToken",
                table: "Servers",
                type: "varchar(36)",
                maxLength: 36,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsRegistryValidated",
                table: "Servers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedHostname",
                table: "Servers",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "RetiredAt",
                table: "Servers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetiredByUserId",
                table: "Servers",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Providers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "DnsProviders",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InfrastructureChangeLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ResourceType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResourceId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActorUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BeforeJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AfterJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfrastructureChangeLogs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ServiceAuditRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RequestedByUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AuditedHostnameCount = table.Column<int>(type: "int", nullable: false),
                    ZoneCount = table.Column<int>(type: "int", nullable: false),
                    RecordCount = table.Column<int>(type: "int", nullable: false),
                    AvailabilityCheckedCount = table.Column<int>(type: "int", nullable: false),
                    AvailabilityHealthyCount = table.Column<int>(type: "int", nullable: false),
                    CriticalCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    WarningCount = table.Column<int>(type: "int", nullable: false),
                    InfoCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAuditRuns", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ServiceAuditIssues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ServiceAuditRunId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: true),
                    DomainAliasId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Severity = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Domain = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Details = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ObservedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAuditIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceAuditIssues_ServiceAuditRuns_ServiceAuditRunId",
                        column: x => x.ServiceAuditRunId,
                        principalTable: "ServiceAuditRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ServiceAuditObservations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ServiceAuditRunId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: true),
                    Domain = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Health = table.Column<int>(type: "int", nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    Details = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ObservedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAuditObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceAuditObservations_ServiceAuditRuns_ServiceAuditRunId",
                        column: x => x.ServiceAuditRunId,
                        principalTable: "ServiceAuditRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Services_NormalizedPrimaryDomain",
                table: "Services",
                column: "NormalizedPrimaryDomain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Servers_NormalizedHostname",
                table: "Servers",
                column: "NormalizedHostname",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Providers_NormalizedName",
                table: "Providers",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DnsProviders_NormalizedName",
                table: "DnsProviders",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InfrastructureChangeLogs_CreatedAt",
                table: "InfrastructureChangeLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InfrastructureChangeLogs_ResourceType_ResourceId_CreatedAt",
                table: "InfrastructureChangeLogs",
                columns: new[] { "ResourceType", "ResourceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAuditIssues_ServiceAuditRunId_Severity",
                table: "ServiceAuditIssues",
                columns: new[] { "ServiceAuditRunId", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAuditIssues_ServiceId",
                table: "ServiceAuditIssues",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAuditObservations_ServiceAuditRunId_ServiceId",
                table: "ServiceAuditObservations",
                columns: new[] { "ServiceAuditRunId", "ServiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAuditRuns_CompletedAt",
                table: "ServiceAuditRuns",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAuditRuns_Status_RequestedAt",
                table: "ServiceAuditRuns",
                columns: new[] { "Status", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InfrastructureChangeLogs");

            migrationBuilder.DropTable(
                name: "ServiceAuditIssues");

            migrationBuilder.DropTable(
                name: "ServiceAuditObservations");

            migrationBuilder.DropTable(
                name: "ServiceAuditRuns");

            migrationBuilder.DropIndex(
                name: "IX_Services_NormalizedPrimaryDomain",
                table: "Services");

            migrationBuilder.DropIndex(
                name: "IX_Servers_NormalizedHostname",
                table: "Servers");

            migrationBuilder.DropIndex(
                name: "IX_Providers_NormalizedName",
                table: "Providers");

            migrationBuilder.DropIndex(
                name: "IX_DnsProviders_NormalizedName",
                table: "DnsProviders");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "IsRegistryValidated",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "NormalizedPrimaryDomain",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "RetiredAt",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "RetiredByUserId",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "IsRegistryValidated",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "NormalizedHostname",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "RetiredAt",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "RetiredByUserId",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "DnsProviders");
        }
    }
}
