using System.Security.Claims;
using Aiursoft.CSTools.Tools;
using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services.Export;
using Aiursoft.Scanner.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.BackgroundJobs;

public class AutoExportJob(
    ILogger<AutoExportJob> logger,
    IServiceScopeFactory scopeFactory)
    : IHostedService, IDisposable, ISingletonDependency
{
    private const int IntervalHours = 8;
    private const int StartupDelaySeconds = 45;
    private Timer? _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!EntryExtends.IsProgramEntry())
        {
            return Task.CompletedTask;
        }

        logger.LogInformation("Auto Export Background Service is starting. Will run every {Interval} hours.", IntervalHours);

        _timer = new Timer(
            DoWork,
            null,
            TimeSpan.FromSeconds(StartupDelaySeconds),
            TimeSpan.FromHours(IntervalHours));

        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
    {
        try
        {
            logger.LogInformation("Auto export job started at {Time}", DateTime.UtcNow);
            using var scope = scopeFactory.CreateScope();
            
            var dbContext = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var exportService = scope.ServiceProvider.GetRequiredService<ExportService>();
            var pathResolver = scope.ServiceProvider.GetRequiredService<ExportPathResolver>();

            var allUsers = await dbContext.Users.ToListAsync();
            var autoExportRoot = pathResolver.GetAutoExportRoot();

            foreach (var user in allUsers)
            {
                try
                {
                    // Create a ClaimsPrincipal for the user to honor permission logic
                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.NameIdentifier, user.Id),
                        new(ClaimTypes.Name, user.UserName ?? user.Email!)
                    };

                    // Add all permissions for this user
                    var userClaims = await userManager.GetClaimsAsync(user);
                    var userPermissions = userClaims.Where(c => c.Type == AppPermissions.Type);
                    claims.AddRange(userPermissions);

                    // Add roles and their permissions
                    var roles = await userManager.GetRolesAsync(user);
                    foreach (var roleName in roles)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, roleName));
                    }
                    
                    var identity = new ClaimsIdentity(claims, "Identity.Application");
                    var principal = new ClaimsPrincipal(identity);

                    var userAutoExportPath = Path.Combine(autoExportRoot, user.Id);
                    await exportService.ExportAllForUser(principal, userAutoExportPath);
                    logger.LogInformation("Successfully auto-exported data for user {UserEmail}", user.Email);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to auto-export data for user {UserEmail}", user.Email);
                }
            }
            
            logger.LogInformation("Auto export job completed at {Time}", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred in auto export job");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Auto Export Background Service is stopping");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
