using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.CertificateViewModels;

public class PrintViewModel : UiStackLayoutViewModel
{
    public PrintViewModel()
    {
        PageTitle = "Print";
    }

    [Display(Name = "Target User")]
    public User? TargetUser { get; set; }

    [Display(Name = "Type")]
    public CertificateType Type { get; set; }

    [Display(Name = "Certificate Language")]
    public string CertificateLanguage { get; set; } = "zh-CN";

    [Display(Name = "Company Name")]
    public string CompanyName { get; set; } = string.Empty;

    [Display(Name = "Company Name English")]
    public string? CompanyNameEnglish { get; set; }
}
