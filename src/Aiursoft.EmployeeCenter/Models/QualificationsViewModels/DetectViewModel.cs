using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Aiursoft.EmployeeCenter.Models.QualificationsViewModels;

public class DetectViewModel : UiStackLayoutViewModel
{
    public DetectViewModel() => PageTitle = "Detect Qualification Watermark";
    [Required(ErrorMessage = "The image to inspect is required.")]
    [MaxLength(500)]
    [RegularExpression(@"^qualifications/detections/[a-f0-9]{64}/[a-f0-9]{32}\.(png|jpg|jpeg)$", ErrorMessage = "Please upload a valid image to inspect.")]
    [Display(Name = "Image to Inspect")]
    public string? ImagePath { get; set; }

    [BindNever]
    public string UploadSubfolder { get; set; } = string.Empty;

    [BindNever]
    public string? Message { get; set; }

    [BindNever]
    public QualificationDownload? Record { get; set; }
}
