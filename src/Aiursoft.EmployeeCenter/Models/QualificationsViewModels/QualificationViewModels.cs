using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.QualificationsViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel() => PageTitle = "Business Qualifications";
    public List<BusinessQualification> Qualifications { get; set; } = [];
}

public class ManageIndexViewModel : UiStackLayoutViewModel
{
    public ManageIndexViewModel() => PageTitle = "Manage Business Qualifications";
    public List<BusinessQualification> Qualifications { get; set; } = [];
}

public class CreateViewModel : UiStackLayoutViewModel
{
    public CreateViewModel() => PageTitle = "Add Business Qualification";
    [Range(1, int.MaxValue)]
    [Display(Name = "Company Entity")]
    public int CompanyEntityId { get; set; }
    [Required]
    [StringLength(100)]
    [Display(Name = "Qualification Name")]
    public string Name { get; set; } = string.Empty;
    [Required]
    [StringLength(50)]
    [Display(Name = "Version")]
    public string Version { get; set; } = string.Empty;
    [Required]
    [Display(Name = "Original Image")]
    public IFormFile? Original { get; set; }
    public List<CompanyEntity> Companies { get; set; } = [];
}

public class ApplyViewModel : UiStackLayoutViewModel
{
    public ApplyViewModel() => PageTitle = "Generate Watermark and Download";
    public int Id { get; set; }
    [Required]
    [StringLength(120, MinimumLength = 4)]
    [Display(Name = "Business Purpose")]
    public string Purpose { get; set; } = string.Empty;
    [Required]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Recipient Company")]
    public string Recipient { get; set; } = string.Empty;
    public BusinessQualification? Qualification { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
}

public class PreviewViewModel : UiStackLayoutViewModel
{
    public PreviewViewModel() => PageTitle = "Watermarked Qualification Preview";
    public required QualificationDownload Record { get; set; }
}

public class RecordsViewModel : UiStackLayoutViewModel
{
    public RecordsViewModel() => PageTitle = "Qualification Download Records";
    public List<QualificationDownload> Records { get; set; } = [];
    public int Page { get; set; }
    public bool HasNextPage { get; set; }
    public string? Query { get; set; }
}

public class ManageRecordsViewModel : RecordsViewModel
{
    public ManageRecordsViewModel() => PageTitle = "All Qualification Download Records";
}

public class ManageRecordViewModel : UiStackLayoutViewModel
{
    public ManageRecordViewModel() => PageTitle = "Qualification Issuance Record";
    public required QualificationDownload Record { get; set; }
}

public class DetectViewModel : UiStackLayoutViewModel
{
    public DetectViewModel() => PageTitle = "Detect Qualification Watermark";
    [Required]
    [Display(Name = "Image to Inspect")]
    public IFormFile? Image { get; set; }
    public string? Message { get; set; }
    public QualificationDownload? Record { get; set; }
}
