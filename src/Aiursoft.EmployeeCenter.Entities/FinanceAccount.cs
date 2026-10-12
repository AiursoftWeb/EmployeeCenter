using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class FinanceAccount
{
    [Key]
    public int Id { get; init; }

    [MaxLength(200)]
    public required string AccountName { get; set; }

    public FinanceAccountType AccountType { get; set; }

    public int CompanyEntityId { get; set; }

    [ForeignKey(nameof(CompanyEntityId))]
    public CompanyEntity? CompanyEntity { get; set; }

    [MaxLength(10)]
    public required string Currency { get; set; }

    public bool IsArchived { get; set; }

    public bool ShowInDashboard { get; set; } = true;

    public DateTime CreationTime { get; init; } = DateTime.UtcNow;
}
