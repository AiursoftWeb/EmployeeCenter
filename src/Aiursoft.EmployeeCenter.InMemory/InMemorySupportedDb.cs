using Aiursoft.DbTools;
using Aiursoft.DbTools.InMemory;
using Aiursoft.EmployeeCenter.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.EmployeeCenter.InMemory;

public class InMemorySupportedDb : SupportedDatabaseType<EmployeeCenterDbContext>
{
    public override string DbType => "InMemory";

    public override IServiceCollection RegisterFunction(IServiceCollection services, string connectionString)
    {
        return services.AddAiurInMemoryDb<InMemoryContext>();
    }

    public override EmployeeCenterDbContext ContextResolver(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<InMemoryContext>();
    }
}
