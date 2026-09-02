using System.Diagnostics.CodeAnalysis;
using Aiursoft.EmployeeCenter.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Sqlite;

[ExcludeFromCodeCoverage]

public class SqliteContext(DbContextOptions<SqliteContext> options) : EmployeeCenterDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // MySQL rejects CHECK constraints that refer to an auto-increment column.
        // Keep this additional database-level guard only on providers that support it.
        builder.Entity<Service>().ToTable(table => table.HasCheckConstraint(
            "CK_Services_NoSelfAlternative",
            "IsRegistryValidated = 0 OR CrossEntityLinkId IS NULL OR CrossEntityLinkId <> Id"));
    }

    public override Task<bool> CanConnectAsync()
    {
        return Task.FromResult(true);
    }
}
