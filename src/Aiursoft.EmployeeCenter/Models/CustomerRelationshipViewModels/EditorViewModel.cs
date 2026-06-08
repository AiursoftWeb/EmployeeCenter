using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aiursoft.EmployeeCenter.Models.CustomerRelationshipViewModels;

public class EditorViewModel : UiStackLayoutViewModel
{
    public EditorViewModel()
    {
        PageTitle = "Edit Customer Relationship";
    }

    public int? Id { get; set; }

    [Display(Name = "Company Entity")]
    public int? CompanyEntityId { get; set; }

    [MaxLength(100)]
    [Display(Name = "Name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    [Display(Name = "Job Title")]
    public string? JobTitle { get; set; }

    [EmailAddress]
    [MaxLength(100)]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Phone]
    [MaxLength(50)]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [MaxLength(500)]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [Display(Name = "Remark")]
    public string? Remark { get; set; }

    public IEnumerable<SelectListItem> AvailableCompanyEntities { get; set; } = [];

    public bool IsNew => !Id.HasValue;
}
