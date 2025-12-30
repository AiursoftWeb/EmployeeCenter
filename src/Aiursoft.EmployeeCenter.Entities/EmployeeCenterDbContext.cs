using Aiursoft.DbTools;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Entities;

public abstract class TemplateDbContext(DbContextOptions options) : IdentityDbContext<User>(options), ICanMigrate
{
    public virtual Task MigrateAsync(CancellationToken cancellationToken) =>
        Database.MigrateAsync(cancellationToken);

    public virtual Task<bool> CanConnectAsync() =>
        Database.CanConnectAsync();


    public DbSet<Payroll> Payrolls { get; set; }
    public DbSet<SshKey> SshKeys { get; set; }

    public DbSet<Password> Passwords { get; set; }

    public DbSet<PasswordShare> PasswordShares { get; set; }

    // ================= 资产管理 =================
    public DbSet<PhysicalAsset> PhysicalAssets { get; set; }
    public DbSet<PhysicalAssetUsage> PhysicalAssetUsages { get; set; }
    public DbSet<VirtualAsset> VirtualAssets { get; set; }
}
