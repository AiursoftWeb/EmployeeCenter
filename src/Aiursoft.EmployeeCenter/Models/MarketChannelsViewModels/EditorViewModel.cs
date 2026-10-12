using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.MarketChannelsViewModels;

public class EditorViewModel : UiStackLayoutViewModel
{
    public EditorViewModel()
    {
        PageTitle = "Editor";
    }

    public int? Id { get; set; }

    public bool IsNew => Id == null || Id == 0;

    [Required]
    [MaxLength(200)]
    [Display(Name = "Platform Name")]
    public required string Name { get; set; }

    [Required]
    [Display(Name = "Manager")]
    public required string ManagerId { get; set; }

    public IEnumerable<User>? Users { get; set; }

    [Required]
    [MaxLength(200)]
    [Display(Name = "Target Audience")]
    public required string TargetAudience { get; set; }

    [Display(Name = "Remarks (Markdown)")]
    public string? Description { get; set; }

    public bool SavedSuccessfully { get; set; }
}
