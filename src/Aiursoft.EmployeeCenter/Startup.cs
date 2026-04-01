using Aiursoft.Canon;
using Aiursoft.CSTools.Tools;
using Aiursoft.DbTools.Switchable;
using Aiursoft.Scanner;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.WebTools.Abstractions.Models;
using Aiursoft.EmployeeCenter.InMemory;
using Aiursoft.EmployeeCenter.MySql;
using Aiursoft.EmployeeCenter.Services.Authentication;
using Aiursoft.EmployeeCenter.Sqlite;
using Aiursoft.UiStack.Layout;
using Aiursoft.UiStack.Navigation;
using Microsoft.AspNetCore.Mvc.Razor;
using Aiursoft.ClickhouseLoggerProvider;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Aiursoft.EmployeeCenter;

public class Startup : IWebStartup
{
    public void ConfigureServices(IConfiguration configuration, IWebHostEnvironment environment, IServiceCollection services)
    {
        // AppSettings.
        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
        services.Configure<GitLabSettings>(configuration.GetSection("GitLab"));

        // Relational database
        var (connectionString, dbType, allowCache) = configuration.GetDbSettings();
        services.AddSwitchableRelationalDatabase(
            dbType: EntryExtends.IsInUnitTests() ? "InMemory" : dbType,
            connectionString: connectionString,
            supportedDbs:
            [
                new MySqlSupportedDb(allowCache: allowCache, splitQuery: false),
                new SqliteSupportedDb(allowCache: allowCache, splitQuery: true),
                new InMemorySupportedDb()
            ]);

        services.AddLogging(builder =>
        {
            builder.AddClickhouse(options => configuration.GetSection("Logging:Clickhouse").Bind(options));
        });

        // Authentication and Authorization
        services.AddTemplateAuth(configuration);

        // Services
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddTaskCanon();
        services.AddHealthChecks()
            .AddDbContextCheck<Entities.EmployeeCenterDbContext>();

        // Leave Management Services
        services.AddScoped<Services.HolidayService>();
        services.AddScoped<Services.LeaveBalanceService>();

        // Ledger Services
        services.AddScoped<Services.LedgerExchangeRateService>();
        services.AddScoped<Services.LedgerBalanceService>();
        services.AddScoped<Services.LedgerStatisticsService>();

        // Export Services
        services.AddScoped<Services.Export.IDataFetcher, Services.Export.DataFetcher>();
        services.AddScoped<Services.Export.ExportService>();
        services.AddScoped<Services.Export.MarkdownExporter>();
        services.AddScoped<Services.Export.ExportPathResolver>();

        // Background Jobs
        services.AddHostedService<BackgroundJobs.AnnualLeaveAllocationJob>();
        services.AddHostedService<BackgroundJobs.AutoExportJob>();
        services.AddAssemblyDependencies(typeof(Startup).Assembly);
        services.AddSingleton<NavigationState<Startup>>();

        // Background job queue
        services.AddSingleton<Services.BackgroundJobs.BackgroundJobQueue>();
        services.AddHostedService<Services.BackgroundJobs.QueueWorkerService>();

        // Controllers and localization
        services.AddControllersWithViews()
            .AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
                options.SerializerSettings.ContractResolver = new DefaultContractResolver();
            })
            .AddApplicationPart(typeof(Startup).Assembly)
            .AddApplicationPart(typeof(UiStackLayoutViewModel).Assembly)
            .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
            .AddDataAnnotationsLocalization();
    }

    public void Configure(WebApplication app)
    {
        app.UseExceptionHandler("/Error/Code500");
        app.UseStatusCodePagesWithReExecute("/Error/Code{0}");
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapDefaultControllerRoute();
        app.MapHealthChecks("/health");
    }
}
