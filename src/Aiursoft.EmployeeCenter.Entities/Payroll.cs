using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class Payroll
{
    [Key]
    public int Id { get; init; }

    [MaxLength(255)]
    public required string OwnerId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(OwnerId))]
    [NotNull]
    public User? Owner { get; set; }

    public DateTime TargetMonth { get; set; }

    [MaxLength(2000)]
    public required string Content { get; set; }

    // Earnings
    public decimal BaseSalary { get; set; }
    public decimal JobSalary { get; set; }
    public decimal PerformanceBonus { get; set; }
    public decimal Overtime { get; set; }
    public decimal FullAttendance { get; set; }
    public decimal OtherAllowances { get; set; }

    // Deductions
    public decimal LateEarly { get; set; }
    public decimal SickLeave { get; set; }
    public decimal AdministrativeFines { get; set; }

    // Insurances (Personal)
    public decimal PensionPersonal { get; set; }
    public decimal MedicalPersonal { get; set; }
    public decimal UnemploymentPersonal { get; set; }
    public decimal HousingFundPersonal { get; set; }

    // Tax
    public decimal SpecialAdditionalDeduction { get; set; }
    public decimal PersonalIncomeTax { get; set; }

    // Actual
    [MaxLength(10)]
    public string Currency { get; set; } = "CNY";

    public decimal TotalAmount { get; set; }

    /// <summary>
    /// The bank name where this payroll was paid to.
    /// If null, it means it was not paid to a specific bank or paid in cash.
    /// </summary>
    [MaxLength(100)]
    public string? BankName { get; set; }

    /// <summary>
    /// The bank account where this payroll was paid to.
    /// If null, it means it was not paid to a specific bank account.
    /// </summary>
    [MaxLength(100)]
    public string? BankAccount { get; set; }

    // Company Costs
    public decimal PensionCompany { get; set; }
    public decimal MedicalCompany { get; set; }
    public decimal UnemploymentCompany { get; set; }
    public decimal WorkInjuryCompany { get; set; }
    public decimal MaternityCompany { get; set; }
    public decimal HousingFundCompany { get; set; }

    public DateTime CreationTime { get; init; } = DateTime.UtcNow;
}
