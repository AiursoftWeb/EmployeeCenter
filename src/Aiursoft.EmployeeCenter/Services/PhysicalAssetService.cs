using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.Scanner.Abstractions;
using Microsoft.EntityFrameworkCore;
using Aiursoft.DbTools;

namespace Aiursoft.EmployeeCenter.Services;

public class PhysicalAssetService : IScopedDependency
{
    private readonly TemplateDbContext _dbContext;

    public PhysicalAssetService(TemplateDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PhysicalAsset> ApplyAsync(string userId, Guid assetId, string remark)
    {
        var asset = await _dbContext.PhysicalAssets.FindAsync(assetId);
        if (asset == null)
        {
            throw new Exception("Asset not found");
        }

        // 1. 乐观锁检查: (TotalStock - FrozenStock - UsedStock) > 0
        if (asset.TotalStock - asset.FrozenStock - asset.UsedStock <= 0)
        {
            throw new Exception("Stock not sufficient");
        }

        // 2. 增加预占
        asset.FrozenStock++;

        try
        {
            // EF Core Concurrency Check will happen here due to RowVersion
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Retry logic or fail fast
            throw new Exception("Concurrent modification detected. Please try again.");
        }

        // 3. 创建申请记录
        var usage = new PhysicalAssetUsage
        {
            AssetId = assetId,
            UserId = userId,
            Status = AssetStatus.Frozen,
            ApplyTime = DateTime.UtcNow
        };
        _dbContext.PhysicalAssetUsages.Add(usage);

        // 4. Log
        var log = new AssetEventLog
        {
            AssetId = assetId,
            OperatorId = userId,
            FromStatus = AssetStatus.Available,
            ToStatus = AssetStatus.Frozen,
            Remark = $"Apply: {remark}"
        };
        _dbContext.AssetEventLogs.Add(log);

        await _dbContext.SaveChangesAsync();
        return asset;
    }

    public async Task ApproveAsync(string operatorId, Guid usageId, string assignedSerialNumber)
    {
        var usage = await _dbContext.PhysicalAssetUsages
            .Include(u => u.Asset)
            .SingleOrDefaultAsync(u => u.Id == usageId);

        if (usage == null || usage.Status != AssetStatus.Frozen)
        {
            throw new Exception("Invalid usage application");
        }

        var asset = usage.Asset!;

        // 正式扣减: Frozen--, Used++
        asset.FrozenStock--;
        asset.UsedStock++;

        usage.Status = AssetStatus.InUse;
        usage.AssignedSerialNumber = assignedSerialNumber;

        var log = new AssetEventLog
        {
            AssetId = asset.Id,
            OperatorId = operatorId,
            FromStatus = AssetStatus.Frozen,
            ToStatus = AssetStatus.InUse,
            Remark = $"Approved. SN: {assignedSerialNumber}"
        };
        _dbContext.AssetEventLogs.Add(log);

        await _dbContext.SaveChangesAsync();
    }

    public async Task RejectAsync(string operatorId, Guid usageId, string reason)
    {
        var usage = await _dbContext.PhysicalAssetUsages
            .Include(u => u.Asset)
            .SingleOrDefaultAsync(u => u.Id == usageId);

        if (usage == null || usage.Status != AssetStatus.Frozen)
        {
            throw new Exception("Invalid usage application");
        }

        var asset = usage.Asset!;

        // 释放预占
        asset.FrozenStock--;

        // 这里可以选择删除 usage 或者标记为 Rejected
        // 简单起见，这里我们删除它，或者由于没有 Rejected 状态，我们可以加一个 Rejected 状态到 Enum?
        // 用户定义了 Enum: Available, InUse, InRepair, Lost, Frozen. 没有 Rejected.
        // 我们可以硬删除 Usage 记录表示拒绝，或者回退到 Available (但 Usage 主要是记录人与资产关系)
        // 建议：删除 Usage，记录 Log

        _dbContext.PhysicalAssetUsages.Remove(usage);

        var log = new AssetEventLog
        {
            AssetId = asset.Id,
            OperatorId = operatorId,
            FromStatus = AssetStatus.Frozen,
            ToStatus = AssetStatus.Available,
            Remark = $"Rejected: {reason}"
        };
        _dbContext.AssetEventLogs.Add(log);

        await _dbContext.SaveChangesAsync();
    }

    public async Task ReturnAsync(string operatorId, Guid usageId, string remark)
    {
        var usage = await _dbContext.PhysicalAssetUsages
            .Include(u => u.Asset)
            .SingleOrDefaultAsync(u => u.Id == usageId);

        if (usage == null || usage.Status != AssetStatus.InUse)
        {
            throw new Exception("Usage not in InUse status");
        }

        var asset = usage.Asset!;

        // 归还: Used--
        asset.UsedStock--;
        usage.Status = AssetStatus.Available; // Or Completed/Returned? 
        // Usage 表记录的是 "当前的使用情况" 还是 "历史记录"?
        // 如果是历史记录，Status 应该是 Returned. 但 Enum 里没有 Returned.
        // 让我们看看 Enum: Available=0. 
        // 那么 Usage.Status = Available 意味着这条 Usage 结束了? 
        // 或者我们应该保留 Usage 记录但 Status=Available? 
        // Better: Set return time.
        usage.ReturnTime = DateTime.UtcNow;

        // 如果 Usage 是一次性的 session, 归还后可以删除或者归档.
        // 假如我们把 Usage 当作 Session, 那么 ReturnTime 有值且 Status=Available 可能意味着已归还.

        var log = new AssetEventLog
        {
            AssetId = asset.Id,
            OperatorId = operatorId,
            FromStatus = AssetStatus.InUse,
            ToStatus = AssetStatus.Available,
            Remark = $"Returned: {remark}"
        };
        _dbContext.AssetEventLogs.Add(log);

        await _dbContext.SaveChangesAsync();
    }
}
