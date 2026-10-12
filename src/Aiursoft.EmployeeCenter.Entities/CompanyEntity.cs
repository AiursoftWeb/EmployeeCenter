using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Entities;

public class CompanyEntity
{
    [Key]
    public int Id { get; init; }

    [MaxLength(200)]
    public required string CompanyName { get; set; }

    [MaxLength(200)]
    public string? CompanyNameEnglish { get; set; }

    /// <summary>
    /// Mainland: Unified Social Credit Code
    /// HK: BR Number
    /// </summary>
    [MaxLength(50)]
    public required string EntityCode { get; set; }

    /// <summary>
    /// HK: CI Number
    /// </summary>
    [MaxLength(50)]
    public string? CINumber { get; set; }

    [MaxLength(500)]
    public string? RegisteredAddress { get; set; }

    [MaxLength(500)]
    public string? OfficeAddress { get; set; }

    [MaxLength(20)]
    public string? ZipCode { get; set; }

    /// <summary>
    /// Mainland: Legal Representative
    /// HK: Director
    /// </summary>
    [MaxLength(100)]
    public string? LegalRepresentative { get; set; }

    [MaxLength(100)]
    public string? LegalRepresentativeLegalName { get; set; }

    [MaxLength(100)]
    public string? CompanyType { get; set; }

    public DateTime? EstablishmentDate { get; set; }

    /// <summary>
    /// Mainland: Business Term
    /// HK: BR Expiry Date
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    [MaxLength(100)]
    public string? BankName { get; set; }

    [MaxLength(50)]
    public string? BankAccount { get; set; }

    [MaxLength(50)]
    public string? BankAccountName { get; set; }

    [MaxLength(50)]
    public string? SwiftCode { get; set; }

    [MaxLength(50)]
    public string? BankCode { get; set; }

    [MaxLength(500)]
    public string? BankAddress { get; set; }

    [MaxLength(500)]
    public string? LogoPath { get; set; }

    [MaxLength(500)]
    public string? SealPath { get; set; }

    [MaxLength(500)]
    public string? LicensePath { get; set; }

    [MaxLength(500)]
    public string? OrganizationCertificatePath { get; set; }

    // Mainland Specific
    [MaxLength(50)]
    public string? RegisteredCapital { get; set; }

    [MaxLength(50)]
    public string? OperationStatus { get; set; } // Open, Closed

    // HK Specific
    [MaxLength(500)]
    public string? SCRLocation { get; set; }

    [MaxLength(100)]
    public string? CompanySecretary { get; set; }

    [MaxLength(10)]
    public string BaseCurrency { get; set; } = "CNY";

    public bool CreateLedger { get; set; }

    public DateTime CreationTime { get; init; } = DateTime.UtcNow;

    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}
