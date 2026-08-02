using Aiursoft.CSTools.Tools;
using Aiursoft.DbTools.Switchable;
using Aiursoft.Scanner;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.WebTools.Abstractions.Models;
using Aiursoft.EmployeeCenter.InMemory;
using Aiursoft.EmployeeCenter.MySql;
using Aiursoft.EmployeeCenter.Services.Authentication;
using Aiursoft.EmployeeCenter.Sqlite;
using Aiursoft.UiStack;
using Aiursoft.UiStack.Layout;
using Aiursoft.UiStack.Navigation;
using Microsoft.AspNetCore.Mvc.Razor;
using Aiursoft.ClickhouseLoggerProvider;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Aiursoft.Canon.TaskQueue;
using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.Canon.ScheduledTasks;
using Ganss.Xss;
using Markdig;

namespace Aiursoft.EmployeeCenter;

public class Startup : IWebStartup
{
    public void ConfigureServices(IConfiguration configuration, IWebHostEnvironment environment, IServiceCollection services)
    {
        // AppSettings.
        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
        services.Configure<OcrSettings>(configuration.GetSection("AppSettings:OCR"));
        services.Configure<AsrSettings>(configuration.GetSection("AppSettings:ASR"));

        // Validate OCR configuration (Skip in unit tests to avoid failing all tests)
        var ocrSettings = configuration.GetSection("AppSettings:OCR").Get<OcrSettings>();
        if (!EntryExtends.IsInUnitTests() && ocrSettings is { Enabled: true } && (string.IsNullOrEmpty(ocrSettings.Endpoint) || string.IsNullOrEmpty(ocrSettings.BearerToken)))
        {
            throw new InvalidOperationException("OCR is enabled but Endpoint or BearerToken is not configured in AppSettings:OCR. Please configure them or set Enabled to false.");
        }

        var asrSettings = configuration.GetSection("AppSettings:ASR").Get<AsrSettings>();
        if (!EntryExtends.IsInUnitTests() &&
            asrSettings is { Enabled: true } &&
            (string.IsNullOrEmpty(asrSettings.Endpoint) ||
             string.IsNullOrEmpty(asrSettings.SystemEndpoint) ||
             string.IsNullOrEmpty(asrSettings.BearerToken)))
        {
            throw new InvalidOperationException("ASR is enabled but Endpoint, SystemEndpoint, or BearerToken is not configured in AppSettings:ASR. Please configure them or set Enabled to false.");
        }

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
        services.AddEmployeeCenterAuth(configuration);

        // Services
        services.AddMemoryCache();
        services.AddHealthChecks()
            .AddDbContextCheck<Entities.EmployeeCenterDbContext>();

        // Leave Management Services
        services.AddScoped<Services.HolidayService>();
        services.AddScoped<Services.LeaveBalanceService>();

        // Ledger Services
        services.AddScoped<Services.LedgerExchangeRateService>();
        services.AddScoped<Services.LedgerBalanceService>();
        services.AddScoped<Services.LedgerStatisticsService>();

        // Background Jobs (handled by scheduled task engine below)
        services.AddAssemblyDependencies(typeof(Startup).Assembly);

        // Must be registered AFTER AddAssemblyDependencies so the typed HttpClient registration
        // overrides the plain transient registration from ITransientDependency scanning.
        services.AddHttpClient<Services.OcrService>(client =>
        {
            var ocrConfig = configuration.GetSection("AppSettings:OCR").Get<OcrSettings>();
            client.Timeout = TimeSpan.FromSeconds(ocrConfig?.TimeoutSeconds ?? 1800);
        });
        services.AddHttpClient<Services.AsrService>(client =>
        {
            var asrConfig = configuration.GetSection("AppSettings:ASR").Get<AsrSettings>();
            client.Timeout = TimeSpan.FromSeconds(asrConfig?.TimeoutSeconds ?? 7200);
        });
        services.AddHttpClient<Services.MeetingMinutesService>(client =>
        {
            var agentConfig = configuration.GetSection("AppSettings:Agent").Get<AgentSettings>();
            client.Timeout = TimeSpan.FromSeconds(agentConfig?.MeetingMinutesTimeoutSeconds ?? 600);
        });
        services.AddSingleton<NavigationState<Startup>>();

        // Background job queue
        services.AddTaskQueueEngine();
        services.AddScheduledTaskEngine();
        services.RegisterBackgroundJob<Services.BackgroundJobs.DummyJob>();
        var orphanAvatarCleanupJob = services.RegisterBackgroundJob<Services.BackgroundJobs.OrphanAvatarCleanupJob>();
        services.RegisterScheduledTask(registration: orphanAvatarCleanupJob, period: TimeSpan.FromHours(6), startDelay: TimeSpan.FromMinutes(5));
        var annualLeaveJob = services.RegisterBackgroundJob<Services.BackgroundJobs.AnnualLeaveAllocationJob>();
        services.RegisterScheduledTask(registration: annualLeaveJob, period: TimeSpan.FromHours(8), startDelay: TimeSpan.FromSeconds(25));
        
        var contractOcrJob = services.RegisterBackgroundJob<Services.BackgroundJobs.ContractOcrJob>();
        services.RegisterScheduledTask(registration: contractOcrJob, period: TimeSpan.FromHours(12), startDelay: TimeSpan.FromMinutes(15));

        var transactionOcrJob = services.RegisterBackgroundJob<Services.BackgroundJobs.TransactionOcrJob>();
        services.RegisterScheduledTask(registration: transactionOcrJob, period: TimeSpan.FromHours(12), startDelay: TimeSpan.FromMinutes(20));

        var audioAsrJob = services.RegisterBackgroundJob<Services.BackgroundJobs.AudioAsrJob>();
        services.RegisterScheduledTask(registration: audioAsrJob, period: TimeSpan.FromHours(12), startDelay: TimeSpan.FromMinutes(25));

        var meetingMinutesJob = services.RegisterBackgroundJob<Services.BackgroundJobs.MeetingMinutesJob>();
        services.RegisterScheduledTask(registration: meetingMinutesJob, period: TimeSpan.FromMinutes(15), startDelay: TimeSpan.FromMinutes(30));

        var exportJob = services.RegisterBackgroundJob<Services.BackgroundJobs.ExportJob>();
        services.RegisterScheduledTask(registration: exportJob, period: TimeSpan.FromMinutes(15), startDelay: TimeSpan.FromSeconds(35));

        services.AddHttpClient();

        // Add the markdown pipeline and HTML sanitizer
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();
        services.AddSingleton(pipeline);
        services.AddSingleton(_ =>
        {
            var sanitizer = new HtmlSanitizer();
            sanitizer.AllowedTags.Add("br");
            sanitizer.AllowedAttributes.Add("class");
            return sanitizer;
        });

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
        app.UseUIStack();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapDefaultControllerRoute();
        app.MapHealthChecks("/health");
    }
}
