using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ServicesViewModels;

public class ServicesDashboardViewModel : UiStackLayoutViewModel
{
    public ServicesDashboardViewModel()
    {
        PageTitle = "Services Dashboard";
    }

    public int TotalServices { get; set; }
    public int RunningServices { get; set; }
    public int AssignedServices { get; set; }
    public int CloudflareProxiedServices { get; set; }
    public int FrpsServices { get; set; }
    public int AuthentikIntegratedServices { get; set; }
    public int SelfDevelopedServices { get; set; }
    public int ActiveServerCount { get; set; }
    public int ActiveLocationCount { get; set; }
    public bool CanViewAudit { get; set; }
    public double? OperationalPercentage { get; set; }
    public int? OperationalHealthySubjectCount { get; set; }
    public int? OperationalSubjectCount { get; set; }
    public int? DnsAuditCriticalCount { get; set; }
    public int? DnsAuditErrorCount { get; set; }
    public int? DnsAuditWarningCount { get; set; }
    public DateTime? DnsAuditGeneratedAt { get; set; }

    public double AssignmentPercentage => Percentage(AssignedServices);
    public double DnsProviderPercentage { get; set; }
    public double AuthentikPercentage => Percentage(AuthentikIntegratedServices);
    public double SelfDevelopedPercentage => Percentage(SelfDevelopedServices);

    public List<ServiceDashboardDistributionItem> ServerDistribution { get; set; } = new();
    public List<ServiceDashboardDistributionItem> LocationDistribution { get; set; } = new();
    public List<ServiceDashboardDistributionItem> DnsProviderDistribution { get; set; } = new();
    public List<ServiceDashboardDistributionItem> StatusDistribution { get; set; } = new();
    public List<ServiceDashboardDistributionItem> PurposeDistribution { get; set; } = new();

    private double Percentage(int value)
    {
        return TotalServices == 0 ? 0 : Math.Round(value * 100.0 / TotalServices, 1);
    }
}

public class ServiceDashboardDistributionItem
{
    public required string Name { get; set; }
    public int Count { get; set; }
    public int? ServerId { get; set; }
}
