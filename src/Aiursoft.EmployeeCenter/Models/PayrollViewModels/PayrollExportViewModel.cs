using Aiursoft.CSTools.Attributes;

namespace Aiursoft.EmployeeCenter.Models.PayrollViewModels;

public class PayrollExportViewModel
{

    [CsvProperty("Id")]
    public int Id { get; set; }

    [CsvProperty("OwnerId")]
    public string OwnerId { get; set; } = string.Empty;

    // Adding OwnerName for convenience, as requested by "Raw export" usually implies valuable data.
    // However, sticking to the raw fields first. But Owner Name is nice.
    // I will add OwnerName just in case, but keep it optional.
    [CsvProperty("OwnerName")]
    public string OwnerName { get; set; } = string.Empty;

    [CsvProperty("TargetMonth")]
    public DateTime TargetMonth { get; set; }

    [CsvProperty("Content")]
    public string Content { get; set; } = string.Empty;

    // Earnings
    [CsvProperty("BaseSalary")]
    public decimal BaseSalary { get; set; }
    [CsvProperty("JobSalary")]
    public decimal JobSalary { get; set; }
    [CsvProperty("PerformanceBonus")]
    public decimal PerformanceBonus { get; set; }
    [CsvProperty("Overtime")]
    public decimal Overtime { get; set; }
    [CsvProperty("FullAttendance")]
    public decimal FullAttendance { get; set; }
    [CsvProperty("OtherAllowances")]
    public decimal OtherAllowances { get; set; }

    // Deductions
    [CsvProperty("LateEarly")]
    public decimal LateEarly { get; set; }
    [CsvProperty("SickLeave")]
    public decimal SickLeave { get; set; }
    [CsvProperty("AdministrativeFines")]
    public decimal AdministrativeFines { get; set; }

    // Insurances (Personal)
    [CsvProperty("PensionPersonal")]
    public decimal PensionPersonal { get; set; }
    [CsvProperty("MedicalPersonal")]
    public decimal MedicalPersonal { get; set; }
    [CsvProperty("UnemploymentPersonal")]
    public decimal UnemploymentPersonal { get; set; }
    [CsvProperty("HousingFundPersonal")]
    public decimal HousingFundPersonal { get; set; }

    // Tax
    [CsvProperty("SpecialAdditionalDeduction")]
    public decimal SpecialAdditionalDeduction { get; set; }
    [CsvProperty("PersonalIncomeTax")]
    public decimal PersonalIncomeTax { get; set; }

    // Actual
    [CsvProperty("Currency")]
    public string Currency { get; set; } = string.Empty;
    [CsvProperty("TotalAmount")]
    public decimal TotalAmount { get; set; }

    [CsvProperty("BankName")]
    public string? BankName { get; set; }

    [CsvProperty("BankAccount")]
    public string? BankAccount { get; set; }

    // Company Costs
    [CsvProperty("PensionCompany")]
    public decimal PensionCompany { get; set; }
    [CsvProperty("MedicalCompany")]
    public decimal MedicalCompany { get; set; }
    [CsvProperty("UnemploymentCompany")]
    public decimal UnemploymentCompany { get; set; }
    [CsvProperty("WorkInjuryCompany")]
    public decimal WorkInjuryCompany { get; set; }
    [CsvProperty("MaternityCompany")]
    public decimal MaternityCompany { get; set; }
    [CsvProperty("HousingFundCompany")]
    public decimal HousingFundCompany { get; set; }

    [CsvProperty("CreationTime")]
    public DateTime CreationTime { get; set; }
}
