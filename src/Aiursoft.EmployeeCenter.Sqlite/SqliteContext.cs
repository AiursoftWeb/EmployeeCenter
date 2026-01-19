using System.Diagnostics.CodeAnalysis;
using Aiursoft.EmployeeCenter.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Sqlite;

[ExcludeFromCodeCoverage]

public class SqliteContext(DbContextOptions<SqliteContext> options) : EmployeeCenterDbContext(options)
{
    public override Task<bool> CanConnectAsync()
    {
        return Task.FromResult(true);
    }
}

