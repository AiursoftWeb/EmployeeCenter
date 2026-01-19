using System.Diagnostics.CodeAnalysis;
using Aiursoft.EmployeeCenter.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.MySql;

[ExcludeFromCodeCoverage]

public class MySqlContext(DbContextOptions<MySqlContext> options) : TemplateDbContext(options);
