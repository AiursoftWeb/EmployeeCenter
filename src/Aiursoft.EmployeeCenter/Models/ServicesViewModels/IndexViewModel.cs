using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ServicesViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "Index";
    }

    [Display(Name = "Services")]
    public List<Service> Services { get; set; } = new();

    public Server? FilteredServer { get; set; }

    public bool CanViewAudit { get; set; }

    public bool IncludeRetired { get; set; }

    public IReadOnlyDictionary<int, ServiceAuditObservationResult> LatestObservations { get; set; } =
        new Dictionary<int, ServiceAuditObservationResult>();
}
