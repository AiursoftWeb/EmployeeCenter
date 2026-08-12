using System.Diagnostics;
using System.Text;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.EmployeeCenter.Services.GitLab;
using Aiursoft.Scanner.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aiursoft.EmployeeCenter.Services;

public class ExportService(
    EmployeeCenterDbContext db,
    IOptions<AppSettings> appSettings,
    StorageService storageService,
    GitLabService gitLabService,
    ILogger<ExportService> logger) : IScopedDependency
{
    private readonly string _exportRoot = appSettings.Value.ExportPath;

    public async Task ExportAsync()
    {
        logger.LogInformation("Starting export task to {ExportRoot}...", _exportRoot);

        // Clear export directory content instead of deleting the directory itself
        // because the directory itself might be a mount point.
        if (Directory.Exists(_exportRoot))
        {
            foreach (var directory in Directory.GetDirectories(_exportRoot))
            {
                // Don't delete GitProjects directory to allow incremental updates (git pull)
                if (Path.GetFileName(directory) == "GitProjects")
                {
                    continue;
                }
                Directory.Delete(directory, true);
            }
            foreach (var file in Directory.GetFiles(_exportRoot))
            {
                File.Delete(file);
            }
        }
        else
        {
            Directory.CreateDirectory(_exportRoot);
        }

        await ExportBlueprints();
        await ExportContracts();
        await ExportWeeklyReports();
        await ExportMeetingTranscripts();

        await ExportLedger();
        await ExportAssets();
        await ExportIntangibleAssets();
        await ExportOrganization();
        await ExportServices();
        await ExportServers();
        await ExportMarketChannels();
        await ExportCustomerRelationships();
        await ExportCompanyEntities();
        await ExportGlobalSettings();
        try
        {
            await ExportGitProjects();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export GitLab projects. Other exports have completed successfully.");
        }

        logger.LogInformation("Export task completed successfully.");
    }

    private async Task ExportGitProjects()
    {
        logger.LogInformation("Exporting GitLab projects...");
        var projects = await gitLabService.GetAllProjectsAsync();
        var dir = Path.Combine(_exportRoot, "GitProjects");
        Directory.CreateDirectory(dir);

        // 1. Clean up old projects that are no longer in GitLab
        var activeProjectFolders = projects.Select(p => SanitizeFileName(p.Name)).ToHashSet();
        foreach (var existingDir in Directory.GetDirectories(dir))
        {
            var folderName = Path.GetFileName(existingDir);
            if (!activeProjectFolders.Contains(folderName))
            {
                logger.LogInformation("Removing old project directory {FolderName}...", folderName);
                Directory.Delete(existingDir, true);
            }
        }

        // 2. Clone or Pull
        foreach (var project in projects)
        {
            var projectName = SanitizeFileName(project.Name);
            var projectDir = Path.Combine(dir, projectName);
            
            if (Directory.Exists(Path.Combine(projectDir, ".git")))
            {
                logger.LogInformation("Updating project {ProjectName}...", project.Name);
                await RunGitCommand("fetch --all", projectDir, project.Name);
                await RunGitCommand($"reset --hard origin/{project.DefaultBranch}", projectDir, project.Name);
            }
            else 
            {
                logger.LogInformation("Cloning project {ProjectName}...", project.Name);
                if (Directory.Exists(projectDir))
                {
                    Directory.Delete(projectDir, true);
                }
                await RunGitCommand($"clone --depth 1 {project.HttpUrlToRepo} \"{projectDir}\"", dir, project.Name);
            }
        }
    }

    private async Task RunGitCommand(string args, string workingDir, string projectName)
    {
        try 
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                logger.LogError("Git command '{Args}' failed for project {ProjectName}. Exit code: {ExitCode}. Error: {Error}", args, projectName, process.ExitCode, error);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred during git command '{Args}' for project {ProjectName}", args, projectName);
        }
    }

    private async Task ExportCompanyEntities()
    {
        logger.LogInformation("Exporting company entities...");
        var entities = await db.CompanyEntities.ToListAsync();
        var dir = Path.Combine(_exportRoot, "CompanyEntities");
        Directory.CreateDirectory(dir);

        foreach (var entity in entities)
        {
            var fileName = SanitizeFileName(entity.CompanyName) + ".md";
            var sb = new StringBuilder();
            sb.AppendLine(ObjectToMarkdown(entity, $"Company Entity: {entity.CompanyName}"));

            if (entity.CreateLedger)
            {
                sb.AppendLine("\n## 发票开具与报销指引 (Invoice & Reimbursement Guide)");
                sb.AppendLine("\n### 给员工的操作指引");
                sb.AppendLine("1. **首选全电发票 (数电票)**: 优先让商家开具电子发票（PDF或OFD格式），直接发送到员工邮箱或公司指定财务邮箱 (employees@aiursoft.com)。避免纸质发票，容易丢失且邮寄麻烦。");
                sb.AppendLine("2. **报销类目建议**: 重点类目：餐饮费、交通费、通讯费、电子设备（电脑/外设）。注意：必须是真实发生的费用，严禁虚假虚开，这是红线。");
                sb.AppendLine("3. **特殊情况**: 如果是住宿费（专票），必须在前台开具，且通常需要公司抬头的全称。定额发票（手撕票）金额较小可以使用，但尽量减少。");
                sb.AppendLine("\n**提示**: 大家外出如果产生业务相关或符合福利政策的费用，请务必开具发票。优先要电子发票。记得把PDF原件留好，不要只截图。");
            }

            var servers = await db.Servers.Where(s => s.CompanyEntityId == entity.Id).ToListAsync();
            if (servers.Any())
            {
                sb.AppendLine("\n## 关联服务器 (Associated Servers)");
                foreach (var server in servers)
                {
                    sb.AppendLine($"- {server.Hostname} ({server.ServerIp})");
                }
            }

            var intangibleAssets = await db.IntangibleAssets.Where(a => a.CompanyEntityId == entity.Id).ToListAsync();
            if (intangibleAssets.Any())
            {
                sb.AppendLine("\n## 关联的无形资产 (Associated Intangible Assets)");
                foreach (var asset in intangibleAssets)
                {
                    sb.AppendLine($"- {asset.Name} ({asset.Type} - {asset.Status})");
                }
            }

            await File.WriteAllTextAsync(Path.Combine(dir, fileName), sb.ToString());
        }
    }

    private async Task ExportGlobalSettings()
    {
        logger.LogInformation("Exporting global settings...");
        var settings = await db.GlobalSettings.ToListAsync();
        var dir = Path.Combine(_exportRoot, "GlobalSettings");
        Directory.CreateDirectory(dir);

        var content = TableToMarkdown(settings, "Global Settings");
        await File.WriteAllTextAsync(Path.Combine(dir, "settings.md"), content);
    }

    private async Task ExportWeeklyReports()
    {
        logger.LogInformation("Exporting weekly reports...");
        var reports = await db.WeeklyReports
            .Include(r => r.User)
            .ToListAsync();

        foreach (var report in reports)
        {
            var weekName = report.WeekStartDate.ToString("yyyy-MM-dd");
            var fullDirectoryPath = Path.Combine(_exportRoot, "WeeklyReports", weekName);
            if (!Directory.Exists(fullDirectoryPath))
            {
                Directory.CreateDirectory(fullDirectoryPath);
            }

            var fileName = SanitizeFileName(report.User.DisplayName) + ".md";
            await File.WriteAllTextAsync(Path.Combine(fullDirectoryPath, fileName), report.Content);
        }
    }

    private async Task ExportMeetingTranscripts()
    {
        logger.LogInformation("Exporting meeting transcripts...");
        var transcripts = await db.AudioAsrResults
            .Include(result => result.Audio)
            .Where(result => result.PlainText != string.Empty)
            .ToListAsync();
        var dir = Path.Combine(_exportRoot, "MeetingTranscripts");
        Directory.CreateDirectory(dir);

        foreach (var transcript in transcripts)
        {
            var fileName = $"{SanitizeFileName(transcript.Audio!.Name)}_{transcript.AudioId}.md";
            await File.WriteAllTextAsync(Path.Combine(dir, fileName), transcript.PlainText);

            if (!string.IsNullOrWhiteSpace(transcript.MeetingMinutesMarkdown))
            {
                var minutesFileName = $"{SanitizeFileName(transcript.Audio.Name)}_{transcript.AudioId}_minutes.md";
                await File.WriteAllTextAsync(Path.Combine(dir, minutesFileName), transcript.MeetingMinutesMarkdown);
            }
        }
    }

    private async Task ExportBlueprints()
    {
        logger.LogInformation("Exporting blueprints...");
        var allFolders = await db.BlueprintFolders.ToListAsync();
        var folderMap = allFolders.ToDictionary(f => f.Id);
        
        var blueprints = await db.Blueprints.ToListAsync();
        foreach (var blueprint in blueprints)
        {
            var pathParts = new List<string>();
            var currentFolderId = blueprint.FolderId;
            while (currentFolderId.HasValue && folderMap.TryGetValue(currentFolderId.Value, out var folder))
            {
                pathParts.Insert(0, SanitizeFileName(folder.Name));
                currentFolderId = folder.ParentFolderId;
            }
            
            var relativePath = Path.Combine(pathParts.ToArray());
            var fullDirectoryPath = Path.Combine(_exportRoot, "Blueprints", relativePath);
            if (!Directory.Exists(fullDirectoryPath))
            {
                Directory.CreateDirectory(fullDirectoryPath);
            }
            
            var fileName = SanitizeFileName(blueprint.Title) + ".md";
            await File.WriteAllTextAsync(Path.Combine(fullDirectoryPath, fileName), blueprint.Content);
        }
    }

    private async Task ExportContracts()
    {
        logger.LogInformation("Exporting contracts...");
        var allFolders = await db.ContractFolders.ToListAsync();
        var folderMap = allFolders.ToDictionary(f => f.Id);
        
        var contracts = await db.Contracts.ToListAsync();
        foreach (var contract in contracts)
        {
            var pathParts = new List<string>();
            var currentFolderId = contract.FolderId;
            while (currentFolderId.HasValue && folderMap.TryGetValue(currentFolderId.Value, out var folder))
            {
                pathParts.Insert(0, SanitizeFileName(folder.Name));
                currentFolderId = folder.ParentFolderId;
            }
            
            var relativePath = Path.Combine(pathParts.ToArray());
            var fullDirectoryPath = Path.Combine(_exportRoot, "Contracts", relativePath);
            if (!Directory.Exists(fullDirectoryPath))
            {
                Directory.CreateDirectory(fullDirectoryPath);
            }

            var baseFileName = SanitizeFileName(contract.Name);
            
            // 1. Export PDF
            var physicalPath = storageService.GetFilePhysicalPath(contract.FilePath);
            if (File.Exists(physicalPath))
            {
                File.Copy(physicalPath, Path.Combine(fullDirectoryPath, baseFileName + ".pdf"), true);
            }
            else
            {
                logger.LogWarning("Physical file not found for contract {ContractName} at {Path}", contract.Name, physicalPath);
            }

            // 2. Export OCR if available
            var ocrResult = await db.ContractOcrResults.FirstOrDefaultAsync(r => r.ContractId == contract.Id);
            if (ocrResult != null && !string.IsNullOrWhiteSpace(ocrResult.PlainText))
            {
                await File.WriteAllTextAsync(Path.Combine(fullDirectoryPath, baseFileName + ".md"), ocrResult.PlainText);
            }
        }
    }

    private async Task ExportLedger()
    {
        logger.LogInformation("Exporting ledger...");
        var accounts = await db.FinanceAccounts.Include(a => a.CompanyEntity).ToListAsync();
        var transactions = await db.Transactions
            .Include(t => t.SourceAccount)
            .Include(t => t.DestinationAccount)
            .ToListAsync();

        foreach (var account in accounts)
        {
            var entityName = SanitizeFileName(account.CompanyEntity?.CompanyName ?? "Unknown Entity");
            var accountName = SanitizeFileName(account.AccountName);
            var dir = Path.Combine(_exportRoot, "Ledger", entityName, accountName);
            Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine(ObjectToMarkdown(account, "Finance Account Details"));
            if (account.CompanyEntity != null)
            {
                sb.AppendLine($"- **Company**: {account.CompanyEntity.CompanyName}");
            }
            await File.WriteAllTextAsync(Path.Combine(dir, "AccountInfo.md"), sb.ToString());

            var accountTransactions = transactions.Where(t => t.SourceAccountId == account.Id || t.DestinationAccountId == account.Id)
                .OrderByDescending(t => t.TransactionTime).ToList();
            var txMarkdown = TransactionsToMarkdown(accountTransactions);
            await File.WriteAllTextAsync(Path.Combine(dir, "Transactions.md"), txMarkdown);

            // 3. Export Attachments
            foreach (var tx in accountTransactions)
            {
                await ExportTransactionAttachments(tx, dir);
            }
        }
    }

    private async Task ExportTransactionAttachments(Transaction tx, string baseDir)
    {
        var attachments = new List<(string? Path, string Name, TransactionAttachmentType Type)>
        {
            (tx.InvoicePath, "Invoice", TransactionAttachmentType.Invoice),
            (tx.MT103Path, "MT103", TransactionAttachmentType.MT103),
            (tx.PaymentVoucherPath, "PaymentVoucher", TransactionAttachmentType.PaymentVoucher)
        };

        if (attachments.All(a => string.IsNullOrEmpty(a.Path)))
        {
            return;
        }

        var txDirName = $"{tx.TransactionTime:yyyy-MM-dd}_{SanitizeFileName(tx.Description)}_{tx.Id}";
        var txDir = Path.Combine(baseDir, "Attachments", txDirName);

        foreach (var (path, name, type) in attachments)
        {
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (!Directory.Exists(txDir))
            {
                Directory.CreateDirectory(txDir);
            }

            // 1. Export File
            var physicalPath = storageService.GetFilePhysicalPath(path);
            if (File.Exists(physicalPath))
            {
                var extension = Path.GetExtension(physicalPath);
                File.Copy(physicalPath, Path.Combine(txDir, name + extension), true);
            }
            else
            {
                logger.LogWarning("Physical file not found for transaction {TxId} attachment {Name} at {Path}", tx.Id, name, physicalPath);
            }

            // 2. Export OCR if available
            var ocrResult = await db.TransactionOcrResults.FirstOrDefaultAsync(r => r.TransactionId == tx.Id && r.AttachmentType == type);
            if (ocrResult != null && !string.IsNullOrWhiteSpace(ocrResult.PlainText))
            {
                await File.WriteAllTextAsync(Path.Combine(txDir, name + ".md"), ocrResult.PlainText);
            }
        }
    }

    private async Task ExportAssets()
    {
        logger.LogInformation("Exporting assets...");
        var assets = await db.Assets
            .Include(a => a.CompanyEntity)
            .Include(a => a.Model).ThenInclude(m => m.Category)
            .ToListAsync();

        foreach (var asset in assets)
        {
            var entityName = SanitizeFileName(asset.CompanyEntity?.CompanyName ?? "Unknown Entity");
            var dir = Path.Combine(_exportRoot, "Assets", entityName);
            Directory.CreateDirectory(dir);

            var fileName = SanitizeFileName(asset.AssetTag) + ".md";
            var sb = new StringBuilder();
            sb.AppendLine(ObjectToMarkdown(asset, $"Asset: {asset.AssetTag}"));
            if (asset.CompanyEntity != null)
            {
                sb.AppendLine($"- **Company**: {asset.CompanyEntity.CompanyName}");
            }
            
            sb.AppendLine("\n\n" + ObjectToMarkdown(asset.Model, "Model Details"));
            sb.AppendLine("\n\n" + ObjectToMarkdown(asset.Model.Category, "Category Details"));
            
            await File.WriteAllTextAsync(Path.Combine(dir, fileName), sb.ToString());
        }
    }

    private async Task ExportIntangibleAssets()
    {
        logger.LogInformation("Exporting intangible assets...");
        var assets = await db.IntangibleAssets.ToListAsync();
        var dir = Path.Combine(_exportRoot, "IntangibleAssets");
        Directory.CreateDirectory(dir);

        foreach (var asset in assets)
        {
            var fileName = SanitizeFileName(asset.Name) + ".md";
            var content = ObjectToMarkdown(asset, $"Intangible Asset: {asset.Name}");
            await File.WriteAllTextAsync(Path.Combine(dir, fileName), content);
        }
    }

    private async Task ExportOrganization()
    {
        logger.LogInformation("Exporting organization and leave information...");
        var users = await db.Users.ToListAsync();
        var userRoles = await db.UserRoles.ToListAsync();
        var roles = await db.Roles.ToListAsync();
        var roleClaims = await db.RoleClaims.Where(rc => rc.ClaimType == "Permission").ToListAsync();
        var leaves = await db.LeaveApplications.ToListAsync();

        foreach (var user in users)
        {
            var userName = SanitizeFileName(user.DisplayName);
            var dir = Path.Combine(_exportRoot, "Organization", userName);
            Directory.CreateDirectory(dir);

            var statusSb = new StringBuilder();
            statusSb.AppendLine(ObjectToMarkdown(user, $"User: {user.DisplayName}"));

            var uRoles = userRoles.Where(ur => ur.UserId == user.Id).Select(ur => ur.RoleId).ToList();
            var uRoleNames = roles.Where(r => uRoles.Contains(r.Id)).Select(r => r.Name).ToList();
            statusSb.AppendLine("\n## Roles");
            foreach(var r in uRoleNames) statusSb.AppendLine($"- {r}");

            var uClaims = roleClaims.Where(rc => uRoles.Contains(rc.RoleId)).Select(rc => rc.ClaimValue).Distinct().ToList();
            statusSb.AppendLine("\n## Permissions");
            foreach(var c in uClaims) statusSb.AppendLine($"- {c}");

            await File.WriteAllTextAsync(Path.Combine(dir, "status.md"), statusSb.ToString());

            var uLeaves = leaves.Where(l => l.UserId == user.Id).OrderByDescending(l => l.StartDate).ToList();
            var leavesMd = TableToMarkdown(uLeaves, "Leave History");
            await File.WriteAllTextAsync(Path.Combine(dir, "LeaveHistory.md"), leavesMd);
        }
    }

    private async Task ExportServices()
    {
        logger.LogInformation("Exporting services...");
        var services = await db.Services.ToListAsync();
        var dir = Path.Combine(_exportRoot, "Services");
        Directory.CreateDirectory(dir);

        foreach (var service in services)
        {
            var fileName = SanitizeFileName(service.Domain) + ".md";
            var content = ObjectToMarkdown(service, $"Service: {service.Domain}");
            await File.WriteAllTextAsync(Path.Combine(dir, fileName), content);
        }
    }

    private async Task ExportServers()
    {
        logger.LogInformation("Exporting servers...");
        var servers = await db.Servers
            .Include(s => s.CompanyEntity)
            .ToListAsync();
        var dir = Path.Combine(_exportRoot, "Servers");
        Directory.CreateDirectory(dir);

        foreach (var server in servers)
        {
            var name = !string.IsNullOrWhiteSpace(server.Hostname) ? server.Hostname : server.ServerIp ?? server.Id.ToString();
            var fileName = SanitizeFileName(name) + ".md";
            var sb = new StringBuilder();
            sb.AppendLine(ObjectToMarkdown(server, $"Server: {name}"));
            if (server.CompanyEntity != null)
            {
                sb.AppendLine($"- **Company**: {server.CompanyEntity.CompanyName}");
            }
            await File.WriteAllTextAsync(Path.Combine(dir, fileName), sb.ToString());
        }
    }

    private async Task ExportMarketChannels()
    {
        logger.LogInformation("Exporting market channels...");
        var channels = await db.MarketChannels.ToListAsync();
        var dir = Path.Combine(_exportRoot, "MarketChannels");
        Directory.CreateDirectory(dir);

        foreach (var channel in channels)
        {
            var fileName = SanitizeFileName(channel.Name) + ".md";
            var content = ObjectToMarkdown(channel, $"Market Channel: {channel.Name}");
            if (!string.IsNullOrWhiteSpace(channel.Description))
            {
                content += "\n\n## Description\n\n" + channel.Description;
            }
            await File.WriteAllTextAsync(Path.Combine(dir, fileName), content);
        }
    }

    private async Task ExportCustomerRelationships()
    {
        logger.LogInformation("Exporting customer relationships...");
        var customers = await db.CustomerRelationships.ToListAsync();
        var dir = Path.Combine(_exportRoot, "CustomerRelationships");
        Directory.CreateDirectory(dir);

        foreach (var customer in customers)
        {
            var fileName = SanitizeFileName(customer.Name) + ".md";
            var content = ObjectToMarkdown(customer, $"Customer: {customer.Name}");
            if (!string.IsNullOrWhiteSpace(customer.Remark))
            {
                content += "\n\n## Remark\n\n" + customer.Remark;
            }
            await File.WriteAllTextAsync(Path.Combine(dir, fileName), content);
        }
    }

    private bool IsSimpleType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            type = Nullable.GetUnderlyingType(type)!;
        }
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(Guid);
    }

    private string ObjectToMarkdown(object? obj, string title = "Details")
    {
        if (obj == null) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        
        var type = obj.GetType();
        foreach (var p in type.GetProperties().Where(p => IsSimpleType(p.PropertyType)))
        {
            try 
            {
                var val = p.GetValue(obj);
                if (val != null)
                {
                    sb.AppendLine($"- **{p.Name}**: {val.ToString()?.Replace("\n", " ").Replace("\r", "")}");
                }
            }
            catch 
            {
                // Ignored
            }
        }
        return sb.ToString();
    }

    private string TransactionsToMarkdown(List<Transaction> transactions, string title = "Transactions")
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();

        if (!transactions.Any())
        {
            sb.AppendLine("No records found.");
            return sb.ToString();
        }

        sb.AppendLine("| Date | Description | From | To | Amount | Exchange Rate | Invoice | MT103 | Voucher |");
        sb.AppendLine("|------|-------------|------|----|--------|---------------|---------|-------|---------|");

        foreach (var t in transactions)
        {
            var amountStr = $"{t.Amount:N2} {t.SourceAccount?.Currency}";
            if (t.ExchangeRate != 1)
            {
                var destAmount = t.Amount * t.ExchangeRate;
                amountStr += $" ({destAmount:N2} {t.DestinationAccount?.Currency})";
            }

            var rateStr = t.ExchangeRate != 1 ? t.ExchangeRate.ToString("N4") : "-";

            var invoiceStr = string.IsNullOrEmpty(t.InvoicePath) ? "-" : "[Invoice](" + t.InvoicePath + ")";
            var mt103Str = string.IsNullOrEmpty(t.MT103Path) ? "-" : "[MT103](" + t.MT103Path + ")";
            var voucherStr = string.IsNullOrEmpty(t.PaymentVoucherPath) ? "-" : "[Voucher](" + t.PaymentVoucherPath + ")";

            sb.AppendLine($"| {t.TransactionTime:yyyy-MM-dd HH:mm} | {t.Description} | {t.SourceAccount?.AccountName} | {t.DestinationAccount?.AccountName} | {amountStr} | {rateStr} | {invoiceStr} | {mt103Str} | {voucherStr} |");
        }

        return sb.ToString();
    }

    private string TableToMarkdown<T>(IEnumerable<T> items, string title = "List")
    {
        var list = items.ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        
        if (!list.Any())
        {
            sb.AppendLine("No records found.");
            return sb.ToString();
        }
        
        var properties = typeof(T).GetProperties().Where(p => IsSimpleType(p.PropertyType)).ToList();
            
        sb.AppendLine("| " + string.Join(" | ", properties.Select(p => p.Name)) + " |");
        sb.AppendLine("|" + string.Join("|", properties.Select(_ => "---")) + "|");
        
        foreach (var item in list)
        {
            var values = properties.Select(p => 
            {
                try 
                {
                    var val = p.GetValue(item);
                    return val?.ToString()?.Replace("\n", " ").Replace("\r", "") ?? string.Empty;
                }
                catch 
                {
                    return string.Empty; 
                }
            });
            sb.AppendLine("| " + string.Join(" | ", values) + " |");
        }
        
        return sb.ToString();
    }

    private string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(name
            .Select(c => invalidChars.Contains(c) ? '_' : c)
            .ToArray());
    }
}
