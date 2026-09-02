using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
{
    /// <inheritdoc />
    public partial class InfrastructureRegistryValidatedConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Services_NoSelfAlternative",
                table: "Services",
                sql: "IsRegistryValidated = 0 OR CrossEntityLinkId IS NULL OR CrossEntityLinkId <> Id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Services_ValidatedFrps",
                table: "Services",
                sql: "IsRegistryValidated = 0 OR IsViaFrps = 0 OR (ServerId IS NOT NULL AND FrpsServerId IS NOT NULL AND ServerId <> FrpsServerId)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Servers_ValidatedIdentifier",
                table: "Servers",
                sql: "IsRegistryValidated = 0 OR NULLIF(TRIM(Hostname), '') IS NOT NULL OR NULLIF(TRIM(ServerIp), '') IS NOT NULL OR NULLIF(TRIM(Ipv6Address), '') IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Services_NoSelfAlternative",
                table: "Services");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Services_ValidatedFrps",
                table: "Services");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Servers_ValidatedIdentifier",
                table: "Servers");
        }
    }
}
