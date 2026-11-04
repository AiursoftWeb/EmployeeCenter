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
        foreach (var entry in ChangeTracker.Entries<Contract>().Where(Changed).ToList())
        {
            if (entry.State == EntityState.Modified && entry.Property(c => c.HistoryKey).IsModified)
                throw new InvalidOperationException("A contract's history key cannot be changed.");
            logs.Add(NewLog(entry.Entity.HistoryKey, entry.Entity.Name,
                entry.State == EntityState.Added ? "Uploaded" : entry.State == EntityState.Deleted ? "Deleted" : "Updated",
                entry.State == EntityState.Added ? null : Snapshot(entry.OriginalValues),
                entry.State == EntityState.Deleted ? null : Snapshot(entry.CurrentValues)));
        }
        foreach (var entry in ChangeTracker.Entries<ContractFolder>().Where(Changed).ToList())
        {
            logs.Add(NewLog(null, entry.Entity.Name,
                entry.State == EntityState.Added ? "Folder created" : entry.State == EntityState.Deleted ? "Folder deleted" : "Folder updated",
                entry.State == EntityState.Added ? null : Snapshot(entry.OriginalValues),
                entry.State == EntityState.Deleted ? null : Snapshot(entry.CurrentValues)));
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
    private static string Snapshot(PropertyValues values) => JsonSerializer.Serialize(
        values.Properties.Where(p => p.Name is not ("Id" or "HistoryKey" or "ContractId" or "JsonResult"))
            .ToDictionary(p => p.Name, p => values[p]));
}
