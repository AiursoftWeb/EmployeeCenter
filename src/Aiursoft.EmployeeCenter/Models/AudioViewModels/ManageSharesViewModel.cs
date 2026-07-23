using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;
using Microsoft.AspNetCore.Identity;

namespace Aiursoft.EmployeeCenter.Models.AudioViewModels;

public class ManageSharesViewModel : UiStackLayoutViewModel
{
    public ManageSharesViewModel()
    {
        PageTitle = "Manage Audio Shares";
    }

    [Display(Name = "Audio Id")]
    public int AudioId { get; set; }

    [Display(Name = "Audio Name")]
    public string? AudioName { get; set; }

    [Display(Name = "Existing Shares")]
    public List<AudioShare> ExistingShares { get; set; } = [];

    [Display(Name = "Available Roles")]
    public List<IdentityRole> AvailableRoles { get; set; } = [];
}
