using System.Diagnostics.CodeAnalysis;
using Aiursoft.DbTools;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Entities;

[ExcludeFromCodeCoverage]
public abstract class EmployeeCenterDbContext(DbContextOptions options) : IdentityDbContext<User>(options), ICanMigrate
{
    /// <summary>
    /// Stores global system configurations as key-value pairs.
    /// </summary>
    public DbSet<GlobalSetting> GlobalSettings => Set<GlobalSetting>();

    /// <summary>
    /// Manages employee monthly salary records, including earnings, deductions, insurance contributions, and taxes.
    /// </summary>
    public DbSet<Payroll> Payrolls => Set<Payroll>();

    /// <summary>
    /// Stores public SSH keys for employees, used for server or repository access.
    /// </summary>
    public DbSet<SshKey> SshKeys => Set<SshKey>();

    /// <summary>
    /// Secure storage for shared accounts and passwords, including secrets and optional attachments.
    /// </summary>
    public DbSet<Password> Passwords => Set<Password>();

    /// <summary>
    /// Manages permissions for shared passwords, defining which users or roles can access them.
    /// </summary>
    public DbSet<PasswordShare> PasswordShares => Set<PasswordShare>();

    /// <summary>
    /// Tracks system or operational incidents, including severity, status, and post-mortem details.
    /// </summary>
    public DbSet<Incident> Incidents => Set<Incident>();

    /// <summary>
    /// Stores comments and system-generated logs related to specific incidents.
    /// </summary>
    public DbSet<IncidentComment> IncidentComments => Set<IncidentComment>();

    /// <summary>
    /// Records history of changes to users' bank account information for payroll auditing.
    /// </summary>
    public DbSet<BankCardChangeLog> BankCardChangeLogs => Set<BankCardChangeLog>();

    /// <summary>
    /// Tracks annual and sick leave allocations for employees, including carried-over leave.
    /// </summary>
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();

    /// <summary>
    /// Manages employee leave requests, tracking dates, approval status, and reviewer details.
    /// </summary>
    public DbSet<LeaveApplication> LeaveApplications => Set<LeaveApplication>();

    /// <summary>
    /// Defines the sequence of tasks for new employee onboarding.
    /// </summary>
    public DbSet<OnboardingTask> OnboardingTasks => Set<OnboardingTask>();

    /// <summary>
    /// Tracks the progress and completion of onboarding tasks for each user.
    /// </summary>
    public DbSet<OnboardingTaskLog> OnboardingTaskLogs => Set<OnboardingTaskLog>();

    /// <summary>
    /// Stores legal entity information for the company, supporting multiple jurisdictions and tax details.
    /// </summary>
    public DbSet<CompanyEntity> CompanyEntities => Set<CompanyEntity>();

    /// <summary>
    /// Audit logs for changes made to company entity records.
    /// </summary>
    public DbSet<CompanyEntityLog> CompanyEntityLogs => Set<CompanyEntityLog>();

    /// <summary>
    /// Manages financial accounts (bank, cash, etc.) associated with company entities.
    /// </summary>
    public DbSet<FinanceAccount> FinanceAccounts => Set<FinanceAccount>();

    /// <summary>
    /// Records financial transfers between accounts, supporting multiple currencies and linked invoices.
    /// </summary>
    public DbSet<Transaction> Transactions => Set<Transaction>();

    /// <summary>
    /// Groups physical assets into categories (e.g., Laptops, Monitors).
    /// </summary>
    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();

    /// <summary>
    /// Defines specific hardware models within an asset category.
    /// </summary>
    public DbSet<AssetModel> AssetModels => Set<AssetModel>();

    /// <summary>
    /// Stores physical locations where assets or servers can be situated.
    /// </summary>
    public DbSet<Location> Locations => Set<Location>();

    /// <summary>
    /// Tracks suppliers from whom assets or services are purchased.
    /// </summary>
    public DbSet<Vendor> Vendors => Set<Vendor>();

    /// <summary>
    /// Tracks physical hardware assets, their status, assignment, and purchase details.
    /// </summary>
    public DbSet<Asset> Assets => Set<Asset>();

    /// <summary>
    /// Audit logs for changes and movements of physical assets.
    /// </summary>
    public DbSet<AssetHistory> AssetHistories => Set<AssetHistory>();

    /// <summary>
    /// Manages non-physical assets like software licenses, domain names, or certificates.
    /// </summary>
    public DbSet<IntangibleAsset> IntangibleAssets => Set<IntangibleAsset>();

    /// <summary>
    /// Stores digital copies of company contracts and tracks their status.
    /// </summary>
    public DbSet<Contract> Contracts => Set<Contract>();

    /// <summary>
    /// Hierarchical folder structure for organizing company contracts.
    /// </summary>
    public DbSet<ContractFolder> ContractFolders => Set<ContractFolder>();

    /// <summary>
    /// Stores results from OCR processing of contract files, including plain text and structured data.
    /// </summary>
    public DbSet<ContractOcrResult> ContractOcrResults => Set<ContractOcrResult>();

    /// <summary>
    /// Defines billing relationships between entities, often linked to contracts for recurring payments.
    /// </summary>
    public DbSet<CollectionChannel> CollectionChannels => Set<CollectionChannel>();

    /// <summary>
    /// Tracks individual payment records (expected vs actual) within a collection channel.
    /// </summary>
    public DbSet<CollectionRecord> CollectionRecords => Set<CollectionRecord>();

    /// <summary>
    /// Records employee career progression, including changes in job levels and titles.
    /// </summary>
    public DbSet<PromotionHistory> PromotionHistories => Set<PromotionHistory>();

    /// <summary>
    /// Stores weekly status updates submitted by employees.
    /// </summary>
    public DbSet<WeeklyReport> WeeklyReports => Set<WeeklyReport>();

    /// <summary>
    /// Links weekly reports to specific requirements or projects to track hours spent.
    /// </summary>
    public DbSet<WeeklyReportRequirement> WeeklyReportRequirements => Set<WeeklyReportRequirement>();

    /// <summary>
    /// Simple personal note-taking space for users.
    /// </summary>
    public DbSet<Notepad> Notepads => Set<Notepad>();

    /// <summary>
    /// Stores internal documentation, guides, or technical blueprints in Markdown format.
    /// </summary>
    public DbSet<Blueprint> Blueprints => Set<Blueprint>();

    /// <summary>
    /// Hierarchical folder structure for organizing blueprints.
    /// </summary>
    public DbSet<BlueprintFolder> BlueprintFolders => Set<BlueprintFolder>();

    /// <summary>
    /// Manages project requirements or feature requests and their lifecycle.
    /// </summary>
    public DbSet<Requirement> Requirements => Set<Requirement>();

    /// <summary>
    /// Facilitates discussion on requirements with threaded comments.
    /// </summary>
    public DbSet<RequirementComment> RequirementComments => Set<RequirementComment>();

    /// <summary>
    /// Lists DNS service providers used by the company.
    /// </summary>
    public DbSet<DnsProvider> DnsProviders => Set<DnsProvider>();

    /// <summary>
    /// Infrastructure providers (Cloud, Data Center) for servers.
    /// </summary>
    public DbSet<Provider> Providers => Set<Provider>();

    /// <summary>
    /// Tracks physical or virtual servers, their network details, and ownership.
    /// </summary>
    public DbSet<Server> Servers => Set<Server>();

    /// <summary>
    /// Manages deployed services, tracking domains, protocols, and integration details.
    /// </summary>
    public DbSet<Service> Services => Set<Service>();

    /// <summary>
    /// Tracks public hostnames that intentionally redirect to a registered service.
    /// </summary>
    public DbSet<DomainAlias> DomainAliases => Set<DomainAlias>();

    /// <summary>
    /// Durable service audit executions and their summaries.
    /// </summary>
    public DbSet<ServiceAuditRun> ServiceAuditRuns => Set<ServiceAuditRun>();

    public DbSet<ServiceAuditIssue> ServiceAuditIssues => Set<ServiceAuditIssue>();

    public DbSet<ServiceAuditObservation> ServiceAuditObservations => Set<ServiceAuditObservation>();

    /// <summary>
    /// Append-only history for infrastructure registry mutations.
    /// </summary>
    public DbSet<InfrastructureChangeLog> InfrastructureChangeLogs => Set<InfrastructureChangeLog>();

    /// <summary>
    /// Stores contact information and details for company customers or external partners.
    /// </summary>
    public DbSet<CustomerRelationship> CustomerRelationships => Set<CustomerRelationship>();

    /// <summary>
    /// Manages marketing channels, target audiences, and responsible managers.
    /// </summary>
    public DbSet<MarketChannel> MarketChannels => Set<MarketChannel>();

    /// <summary>
    /// Defines individual questions for surveys or feedback forms (Signals).
    /// </summary>
    public DbSet<SignalQuestion> SignalQuestions => Set<SignalQuestion>();

    /// <summary>
    /// Collections of questions organized into surveys or questionnaires.
    /// </summary>
    public DbSet<SignalQuestionnaire> SignalQuestionnaires => Set<SignalQuestionnaire>();

    /// <summary>
    /// Mapping table defining the order of questions within a questionnaire.
    /// </summary>
    public DbSet<SignalQuestionnaireQuestion> SignalQuestionnaireQuestions => Set<SignalQuestionnaireQuestion>();

    /// <summary>
    /// Stores user submissions for a specific questionnaire.
    /// </summary>
    public DbSet<SignalResponse> SignalResponses => Set<SignalResponse>();

    /// <summary>
    /// Stores individual answers within a user's questionnaire submission.
    /// </summary>
    public DbSet<SignalQuestionResponse> SignalQuestionResponses => Set<SignalQuestionResponse>();

    /// <summary>
    /// Overrides default holiday rules (e.g., marking weekends as work days).
    /// </summary>
    public DbSet<AdjustedHoliday> AdjustedHolidays => Set<AdjustedHoliday>();

    /// <summary>
    /// Manages employee expense reimbursement requests and their approval status.
    /// </summary>
    public DbSet<Reimbursement> Reimbursements => Set<Reimbursement>();

    /// <summary>
    /// Stores results from OCR processing of transaction attachments (Invoices, MT103s, Payment Vouchers).
    /// </summary>
    public DbSet<TransactionOcrResult> TransactionOcrResults => Set<TransactionOcrResult>();

    /// <summary>
    /// Stores uploaded audio recordings that are transcribed to text via the ASR service.
    /// </summary>
    public DbSet<Audio> Audios => Set<Audio>();

    public DbSet<AudioFileDeletion> AudioFileDeletions => Set<AudioFileDeletion>();

    public DbSet<AudioShare> AudioShares => Set<AudioShare>();

    /// <summary>
    /// Stores plain-text transcripts produced by the ASR service for audio recordings.
    /// </summary>
    public DbSet<AudioAsrResult> AudioAsrResults => Set<AudioAsrResult>();

    public DbSet<AudioAsrSegment> AudioAsrSegments => Set<AudioAsrSegment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ContractOcrResult>()
            .HasIndex(r => r.ContractId)
            .IsUnique();

        builder.Entity<TransactionOcrResult>()
            .HasIndex(r => new { r.TransactionId, r.AttachmentType })
            .IsUnique();

        builder.Entity<Audio>()
            .HasOne(a => a.Owner)
            .WithMany()
            .HasForeignKey(a => a.OwnerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Audio>()
            .HasIndex(a => a.FilePath);

        builder.Entity<Audio>()
            .HasIndex(a => a.PendingFilePath)
            .IsUnique();

        builder.Entity<Audio>()
            .HasIndex(a => new { a.MediaStatus, a.MediaProcessingStartedTime });

        builder.Entity<Audio>()
            .HasIndex(a => new { a.MediaStatus, a.CreateTime });

        builder.Entity<AudioFileDeletion>(entity =>
        {
            entity.HasIndex(deletion => new { deletion.IsDeadLetter, deletion.NextAttemptTime });
        });

        builder.Entity<AudioAsrResult>()
            .HasOne(r => r.Audio)
            .WithOne(a => a.AsrResult)
            .HasForeignKey<AudioAsrResult>(r => r.AudioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AudioAsrResult>()
            .Property(result => result.TranscriptRevision)
            .IsConcurrencyToken();

        builder.Entity<AudioAsrResult>()
            .Property(result => result.CreateTime)
            .IsConcurrencyToken();

        builder.Entity<AudioAsrSegment>(entity =>
        {
            entity.HasKey(segment => new { segment.AudioId, segment.SegmentIndex });
            entity.HasOne(segment => segment.Audio)
                .WithMany(audio => audio.AsrSegments)
                .HasForeignKey(segment => segment.AudioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Audio>()
            .Property(audio => audio.AsrProcessingToken)
            .IsConcurrencyToken();

        builder.Entity<Audio>()
            .Property(audio => audio.MediaProcessingToken)
            .IsConcurrencyToken();

        builder.Entity<AudioShare>(entity =>
        {
            entity.HasOne(s => s.SharedWithUser)
                .WithMany()
                .HasForeignKey(s => s.SharedWithUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(s => s.SharedWithRole)
                .WithMany()
                .HasForeignKey(s => s.SharedWithRoleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(s => new { s.AudioId, s.SharedWithUserId }).IsUnique();
            entity.HasIndex(s => new { s.AudioId, s.SharedWithRoleId }).IsUnique();
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_AudioShares_ExactlyOneRecipient",
                "(SharedWithUserId IS NOT NULL AND SharedWithRoleId IS NULL) OR (SharedWithUserId IS NULL AND SharedWithRoleId IS NOT NULL)"));
        });

        builder.Entity<ContractFolder>()
            .HasIndex(f => new { f.ParentFolderId, f.Name })
            .IsUnique();

        builder.Entity<BlueprintFolder>()
            .HasIndex(f => new { f.ParentFolderId, f.Name })
            .IsUnique();

        builder.Entity<DomainAlias>(entity =>
        {
            entity.HasIndex(alias => alias.Domain).IsUnique();
            entity.Property(alias => alias.Type)
                .HasDefaultValue(DomainAliasType.HttpRedirect);
            entity.HasOne(alias => alias.TargetService)
                .WithMany(service => service.DomainAliases)
                .HasForeignKey(alias => alias.TargetServiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Service>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Services_ValidatedFrps",
                    "IsRegistryValidated = 0 OR IsViaFrps = 0 OR " +
                    "(ServerId IS NOT NULL AND FrpsServerId IS NOT NULL AND ServerId <> FrpsServerId)");
                table.HasCheckConstraint(
                    "CK_Services_NoSelfAlternative",
                    "IsRegistryValidated = 0 OR CrossEntityLinkId IS NULL OR CrossEntityLinkId <> Id");
            });
            entity.Property(service => service.IsAvailabilityAuditEnabled)
                .HasDefaultValue(true);
            entity.Property(service => service.IsRegistryValidated)
                .HasDefaultValue(false);
            entity.Property(service => service.ConcurrencyToken)
                .IsConcurrencyToken();
            entity.HasIndex(service => service.NormalizedPrimaryDomain)
                .IsUnique();
            entity.HasOne(service => service.CompanyEntity)
                .WithMany()
                .HasForeignKey(service => service.CompanyEntityId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(service => service.AlternativeService)
                .WithMany()
                .HasForeignKey(service => service.AlternativeServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        builder.Entity<Server>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Servers_ValidatedIdentifier",
                "IsRegistryValidated = 0 OR " +
                "NULLIF(TRIM(Hostname), '') IS NOT NULL OR " +
                "NULLIF(TRIM(ServerIp), '') IS NOT NULL OR " +
                "NULLIF(TRIM(Ipv6Address), '') IS NOT NULL"));
            entity.Property(server => server.IsRegistryValidated)
                .HasDefaultValue(false);
            entity.Property(server => server.ConcurrencyToken)
                .IsConcurrencyToken();
            entity.HasIndex(server => server.NormalizedHostname)
                .IsUnique();
            entity.HasOne(server => server.TechnicalOwner)
                .WithMany()
                .HasForeignKey(server => server.TechnicalOwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        builder.Entity<Provider>()
            .HasIndex(provider => provider.NormalizedName)
            .IsUnique();

        builder.Entity<DnsProvider>()
            .HasIndex(provider => provider.NormalizedName)
            .IsUnique();

        builder.Entity<ServiceAuditRun>(entity =>
        {
            entity.HasIndex(run => new { run.Status, run.RequestedAt });
            entity.HasIndex(run => run.CompletedAt);
        });

        builder.Entity<ServiceAuditIssue>(entity =>
        {
            entity.HasOne(issue => issue.ServiceAuditRun)
                .WithMany(run => run.Issues)
                .HasForeignKey(issue => issue.ServiceAuditRunId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(issue => new { issue.ServiceAuditRunId, issue.Severity });
            entity.HasIndex(issue => issue.ServiceId);
        });

        builder.Entity<ServiceAuditObservation>(entity =>
        {
            entity.HasOne(observation => observation.ServiceAuditRun)
                .WithMany(run => run.Observations)
                .HasForeignKey(observation => observation.ServiceAuditRunId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(observation => new { observation.ServiceAuditRunId, observation.ServiceId });
        });

        builder.Entity<InfrastructureChangeLog>(entity =>
        {
            entity.HasIndex(log => new { log.ResourceType, log.ResourceId, log.CreatedAt });
            entity.HasIndex(log => log.CreatedAt);
        });
    }

    public virtual Task MigrateAsync(CancellationToken cancellationToken) =>
        Database.MigrateAsync(cancellationToken);

    public virtual Task<bool> CanConnectAsync() =>
        Database.CanConnectAsync();
}
