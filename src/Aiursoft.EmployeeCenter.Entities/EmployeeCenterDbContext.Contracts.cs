using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Aiursoft.EmployeeCenter.Entities;

public abstract partial class EmployeeCenterDbContext
{
    public DbSet<ContractChangeLog> ContractChangeLogs => Set<ContractChangeLog>();
    public string? ContractActorId { get; set; }
    public string ContractActorName { get; set; } = "System";

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var history = RecordContractChanges();
        try { return base.SaveChanges(acceptAllChangesOnSuccess); }
        catch
        {
            foreach (var log in history) Entry(log).State = EntityState.Detached;
            throw;
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var history = RecordContractChanges();
        try { return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
        catch
        {
            foreach (var log in history) Entry(log).State = EntityState.Detached;
            throw;
        }
    }

    private List<ContractChangeLog> RecordContractChanges()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<ContractChangeLog>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Contract history cannot be edited or deleted.");

        var logs = new List<ContractChangeLog>();
        var contracts = ChangeTracker.Entries<Contract>().Where(Changed).ToList();
        var folders = ChangeTracker.Entries<ContractFolder>().Where(Changed).ToList();
        var beforeFolders = new Dictionary<int, (string Name, int? ParentId)>();
        if (contracts.Count != 0 || folders.Count != 0)
            beforeFolders = ContractFolders.AsNoTracking().ToDictionary(f => f.Id, f => (f.Name, f.ParentFolderId));
        var afterFolders = new Dictionary<int, (string Name, int? ParentId)>(beforeFolders);
        foreach (var entry in folders)
        {
            if (entry.State != EntityState.Added)
                beforeFolders[(int)entry.OriginalValues["Id"]!] =
                    ((string)entry.OriginalValues["Name"]!, (int?)entry.OriginalValues["ParentFolderId"]);
            var id = (int)entry.CurrentValues["Id"]!;
            if (entry.State == EntityState.Deleted) afterFolders.Remove(id);
            else afterFolders[id] = (entry.Entity.Name, (int?)entry.CurrentValues["ParentFolderId"]);
        }
        foreach (var entry in contracts)
        {
            if (entry.State == EntityState.Modified && entry.Property(c => c.HistoryKey).IsModified)
                throw new InvalidOperationException("A contract's history key cannot be changed.");
            logs.Add(NewLog(entry.Entity.HistoryKey, entry.Entity.Name,
                entry.State == EntityState.Added ? "Uploaded" : entry.State == EntityState.Deleted ? "Deleted" :
                    entry.Property(c => c.FolderId).IsModified ? "Moved" : "Updated",
                entry.State == EntityState.Added ? null : Snapshot(entry.OriginalValues, beforeFolders),
                entry.State == EntityState.Deleted ? null : Snapshot(entry.CurrentValues, afterFolders)));
        }
        foreach (var entry in folders)
        {
            logs.Add(NewLog(null, entry.Entity.Name,
                entry.State == EntityState.Added ? "Folder created" : entry.State == EntityState.Deleted ? "Folder deleted" : "Folder updated",
                entry.State == EntityState.Added ? null : Snapshot(entry.OriginalValues, beforeFolders, isFolder: true),
                entry.State == EntityState.Deleted ? null : Snapshot(entry.CurrentValues, afterFolders, isFolder: true)));
        }
        foreach (var entry in ChangeTracker.Entries<ContractOcrResult>().Where(Changed).ToList())
        {
            var contract = Contracts.Local.FirstOrDefault(c => c.Id == entry.Entity.ContractId)
                ?? Contracts.Find(entry.Entity.ContractId);
            if (contract == null) continue;
            logs.Add(NewLog(contract.HistoryKey, contract.Name, "OCR updated",
                entry.State == EntityState.Added ? null : Snapshot(entry.OriginalValues),
                entry.State == EntityState.Deleted ? null : Snapshot(entry.CurrentValues)));
        }
        if (folders.Any(e => e.State == EntityState.Modified &&
                (e.Property(f => f.Name).IsModified || e.Property(f => f.ParentFolderId).IsModified)))
        {
            var directlyChanged = contracts.Select(e => e.Entity.HistoryKey).ToHashSet();
            foreach (var contract in Contracts.AsNoTracking().ToList())
            {
                if (directlyChanged.Contains(contract.HistoryKey)) continue;
                var before = FolderPath(contract.FolderId, beforeFolders);
                var after = FolderPath(contract.FolderId, afterFolders);
                if (before == after) continue;
                logs.Add(NewLog(contract.HistoryKey, contract.Name, "Path changed",
                    JsonSerializer.Serialize(new { FolderPath = before }),
                    JsonSerializer.Serialize(new { FolderPath = after })));
            }
        }
        ContractChangeLogs.AddRange(logs);
        return logs;
    }

    private ContractChangeLog NewLog(string? key, string name, string action, string? before, string? after) => new()
    {
        ContractKey = key,
        ContractName = name,
        Action = action,
        UserId = ContractActorId,
        UserName = ContractActorName,
        BeforeJson = before,
        AfterJson = after
    };

    private static bool Changed(EntityEntry entry) => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;
    private static string Snapshot(PropertyValues values,
        Dictionary<int, (string Name, int? ParentId)>? folders = null, bool isFolder = false)
    {
        var snapshot = values.Properties
            .Where(p => p.Name is not ("Id" or "HistoryKey" or "ContractId" or "JsonResult") &&
                        (folders == null || p.Name is not ("FolderId" or "ParentFolderId")))
            .ToDictionary(p => p.Name, p => values[p]);
        if (folders != null)
            snapshot[isFolder ? "Path" : "FolderPath"] = FolderPath(
                isFolder ? (int)values["Id"]! : (int?)values["FolderId"], folders);
        return JsonSerializer.Serialize(snapshot);
    }

    private static string FolderPath(int? id, Dictionary<int, (string Name, int? ParentId)> folders)
    {
        var names = new List<string>();
        var visited = new HashSet<int>();
        while (id.HasValue && folders.TryGetValue(id.Value, out var folder))
        {
            if (!visited.Add(id.Value)) throw new InvalidOperationException("A contract folder path contains a cycle.");
            names.Insert(0, folder.Name);
            id = folder.ParentId;
        }
        return "/" + string.Join("/", names);
    }
}
