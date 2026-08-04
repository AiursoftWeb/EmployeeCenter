using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class IntangibleAsset
{
    [Key]
    public Guid Id { get; init; }

    [MaxLength(100)]
    [Required]
    public required string Name { get; set; }

    public IntangibleAssetType Type { get; set; }

    [MaxLength(200)]
    public string? Supplier { get; set; }

    [MaxLength(100)]
    public string? Account { get; set; }

    [MaxLength(100)]
    public string? Password { get; set; }

    [MaxLength(500)]
    [Url]
    public string? ManagementUrl { get; set; }

    [MaxLength(100)]
    public string? FilingNumber { get; set; }

    [MaxLength(200)]
    public string? FilingSubject { get; set; }

    [MaxLength(1000)]
    public string? FilingQueryMethod { get; set; }

    public DateTime? RegistrationDate { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public decimal? PurchasePrice { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "CNY";

    [MaxLength(500)]
    public string? TrademarkImageUrl { get; set; }

    [MaxLength(500)]
    public string? InvoiceFileUrl { get; set; }

    [MaxLength(500)]
    public string? RegistrationCertificateUrl { get; set; }

    public bool IsPublic { get; set; }

    public int? CompanyEntityId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(CompanyEntityId))]
    public CompanyEntity? CompanyEntity { get; set; }

    [MaxLength(255)]
    public string? AssigneeId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(AssigneeId))]
    public User? Assignee { get; set; }

    public IntangibleAssetStatus Status { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
