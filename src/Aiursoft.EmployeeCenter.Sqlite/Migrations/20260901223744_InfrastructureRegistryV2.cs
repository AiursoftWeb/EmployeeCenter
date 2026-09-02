using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
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
                type: "TEXT",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRegistryValidated",
                table: "Services",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Services",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPrimaryDomain",
                table: "Services",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetiredAt",
                table: "Services",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetiredByUserId",
                table: "Services",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyToken",
                table: "Servers",
                type: "TEXT",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRegistryValidated",
                table: "Servers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedHostname",
                table: "Servers",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetiredAt",
                table: "Servers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetiredByUserId",
                table: "Servers",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Providers",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "DnsProviders",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InfrastructureChangeLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ResourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ActorUserId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    BeforeJson = table.Column<string>(type: "TEXT", nullable: true),
                    AfterJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfrastructureChangeLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceAuditRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RequestedByUserId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    AuditedHostnameCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ZoneCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AvailabilityCheckedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AvailabilityHealthyCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CriticalCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WarningCount = table.Column<int>(type: "INTEGER", nullable: false),
                    InfoCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAuditRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceAuditIssues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServiceAuditRunId = table.Column<long>(type: "INTEGER", nullable: false),
                    ServiceId = table.Column<int>(type: "INTEGER", nullable: true),
                    DomainAliasId = table.Column<int>(type: "INTEGER", nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Domain = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ObservedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "ServiceAuditObservations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServiceAuditRunId = table.Column<long>(type: "INTEGER", nullable: false),
                    ServiceId = table.Column<int>(type: "INTEGER", nullable: true),
                    Domain = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Health = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ObservedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                });

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
