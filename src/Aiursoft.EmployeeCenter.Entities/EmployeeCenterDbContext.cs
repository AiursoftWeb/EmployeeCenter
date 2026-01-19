using System.Diagnostics.CodeAnalysis;
using Aiursoft.DbTools;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Entities;

[ExcludeFromCodeCoverage]
public abstract class EmployeeCenterDbContext(DbContextOptions options) : IdentityDbContext<User>(options), ICanMigrate
{
    public DbSet<GlobalSetting> GlobalSettings => Set<GlobalSetting>();
    public DbSet<Payroll> Payrolls => Set<Payroll>();
    public DbSet<SshKey> SshKeys => Set<SshKey>();
    public DbSet<Password> Passwords => Set<Password>();
    public DbSet<PasswordShare> PasswordShares => Set<PasswordShare>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentComment> IncidentComments => Set<IncidentComment>();
    public DbSet<BankCardChangeLog> BankCardChangeLogs => Set<BankCardChangeLog>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveApplication> LeaveApplications => Set<LeaveApplication>();
    public DbSet<OnboardingTask> OnboardingTasks => Set<OnboardingTask>();
    public DbSet<OnboardingTaskLog> OnboardingTaskLogs => Set<OnboardingTaskLog>();
    public DbSet<CompanyEntity> CompanyEntities => Set<CompanyEntity>();
    public DbSet<CompanyEntityLog> CompanyEntityLogs => Set<CompanyEntityLog>();
    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
    public DbSet<AssetModel> AssetModels => Set<AssetModel>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetHistory> AssetHistories => Set<AssetHistory>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<PromotionHistory> PromotionHistories => Set<PromotionHistory>();
    public DbSet<WeeklyReport> WeeklyReports => Set<WeeklyReport>();
    public DbSet<Notepad> Notepads => Set<Notepad>();

    public DbSet<SignalQuestion> SignalQuestions => Set<SignalQuestion>();
    public DbSet<SignalQuestionnaire> SignalQuestionnaires => Set<SignalQuestionnaire>();
    public DbSet<SignalQuestionnaireQuestion> SignalQuestionnaireQuestions => Set<SignalQuestionnaireQuestion>();
    public DbSet<SignalResponse> SignalResponses => Set<SignalResponse>();
    public DbSet<SignalQuestionResponse> SignalQuestionResponses => Set<SignalQuestionResponse>();

    public virtual Task MigrateAsync(CancellationToken cancellationToken) =>
        Database.MigrateAsync(cancellationToken);

    public virtual Task<bool> CanConnectAsync() =>
        Database.CanConnectAsync();
}

