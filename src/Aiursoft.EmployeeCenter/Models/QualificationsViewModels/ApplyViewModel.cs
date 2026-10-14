using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Aiursoft.EmployeeCenter.Models.QualificationsViewModels;

public class ApplyViewModel : UiStackLayoutViewModel
{
    private string purpose = string.Empty;

    private string recipient = string.Empty;

    public ApplyViewModel() => PageTitle = "Generate Watermark and Download";
    public int Id { get; set; }
    [Required(ErrorMessage = "The business purpose is required.")]
    [StringLength(120, MinimumLength = 4)]
    [Display(Name = "Business Purpose")]
    [PrintableSingleLine]
    [AllowNull]
    public string Purpose
    {
        get => purpose;
        set => purpose = value?.Trim() ?? string.Empty;
    }
    [Required(ErrorMessage = "The recipient company is required.")]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Recipient Company")]
    [PrintableSingleLine]
    [AllowNull]
    public string Recipient
    {
        get => recipient;
        set => recipient = value?.Trim() ?? string.Empty;
    }
    [Required(ErrorMessage = "Select a supported watermark language.")]
    [MaxLength(5)]
    [RegularExpression(@"^(auto|zh-CN|zh-HK|en-US)$", ErrorMessage = "Select a supported watermark language.")]
    [Display(Name = "Watermark Language")]
    public string WatermarkLanguage { get; set; } = "auto";

    [BindNever]
    public BusinessQualification? Qualification { get; set; }

    [BindNever]
    public string ApplicantName { get; set; } = string.Empty;
}
