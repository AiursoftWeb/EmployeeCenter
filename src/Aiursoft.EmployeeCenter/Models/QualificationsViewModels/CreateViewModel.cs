using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Aiursoft.EmployeeCenter.Models.QualificationsViewModels;

public class CreateViewModel : UiStackLayoutViewModel
{
    private string name = string.Empty;

    private string version = string.Empty;

    public CreateViewModel() => PageTitle = "Add Business Qualification";
    [Range(1, int.MaxValue, ErrorMessage = "Please select a valid company entity.")]
    [Display(Name = "Company Entity")]
    public int CompanyEntityId { get; set; }
    [Required(ErrorMessage = "The qualification name is required.")]
    [StringLength(100)]
    [Display(Name = "Qualification Name")]
    [PrintableSingleLine]
    [AllowNull]
    public string Name
    {
        get => name;
        set => name = value?.Trim() ?? string.Empty;
    }
    [Required(ErrorMessage = "The version is required.")]
    [StringLength(50)]
    [Display(Name = "Version")]
    [PrintableSingleLine]
    [AllowNull]
    public string Version
    {
        get => version;
        set => version = value?.Trim() ?? string.Empty;
    }
    [Required(ErrorMessage = "The original image is required.")]
    [MaxLength(500)]
    [RegularExpression(@"^qualifications/uploads/[a-f0-9]{64}/(?:[a-f0-9]{32}/)?[^/\\]+\.(?i:png|jpg|jpeg)$", ErrorMessage = "Please upload a valid qualification original.")]
    [Display(Name = "Original Image")]
    public string? OriginalPath { get; set; }

    [BindNever]
    public List<CompanyEntity> Companies { get; set; } = [];

    [BindNever]
    public string UploadSubfolder { get; set; } = string.Empty;
}
