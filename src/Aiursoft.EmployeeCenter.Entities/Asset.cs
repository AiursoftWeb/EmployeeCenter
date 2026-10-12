using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class Asset
{
    [Key]
    public Guid Id { get; init; }

    [MaxLength(50)]
    public required string AssetTag { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [Required]
    public int ModelId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(ModelId))]
    [NotNull]
    public AssetModel? Model { get; set; }

    [Required]
    public AssetStatus Status { get; set; }

    [MaxLength(255)]
    public string? AssigneeId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(AssigneeId))]
    public User? Assignee { get; set; }

    public int? LocationId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(LocationId))]
    public Location? Location { get; set; }

    public int? CompanyEntityId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(CompanyEntityId))]
    public CompanyEntity? CompanyEntity { get; set; }

    public DateTime? PurchaseDate { get; set; }

    public decimal? PurchasePrice { get; set; }

    public int? VendorId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(VendorId))]
    public Vendor? Vendor { get; set; }

    public DateTime? WarrantyExpireDate { get; set; }

    [MaxLength(500)]
    public string? InvoiceFileUrl { get; set; }

    public bool IsReimbursed { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ConcurrencyCheck]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    [InverseProperty(nameof(AssetHistory.Asset))]
    public IEnumerable<AssetHistory> Histories { get; init; } = new List<AssetHistory>();
}
