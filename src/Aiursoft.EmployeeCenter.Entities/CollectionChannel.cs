using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class CollectionChannel
{
    [Key]
    public int Id { get; set; }

    public int PayerId { get; set; }
    [ForeignKey(nameof(PayerId))]
    public CompanyEntity? Payer { get; set; }

    public int PayeeId { get; set; }
    [ForeignKey(nameof(PayeeId))]
    public CompanyEntity? Payee { get; set; }

    public int? ContractId { get; set; }
    [ForeignKey(nameof(ContractId))]
    public Contract? Contract { get; set; }

    public long ReferenceAmount { get; set; }

    [MaxLength(10)]
    public required string Currency { get; set; }

    [MaxLength(200)]
    public required string PaymentMethod { get; set; }

    public DateTime StartBillingDate { get; set; }

    public DateTime FirstPaymentDate { get; set; }

    public bool IsRecurring { get; set; }

    public RecurringPeriod RecurringPeriod { get; set; }

    public CollectionChannelStatus Status { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    public List<CollectionRecord> Records { get; set; } = [];
}
