using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;
using Microsoft.AspNetCore.Identity;

namespace Aiursoft.EmployeeCenter.Models.PasswordsViewModels;

public class ManageSharesViewModel : UiStackLayoutViewModel
{
    public ManageSharesViewModel()
    {
        PageTitle = "Manage Password Shares";
    }

    [Display(Name = "Password Id")]
    public Guid PasswordId { get; set; }

    [Display(Name = "Password Title")]
    public string? PasswordTitle { get; set; }

    [Display(Name = "Existing Shares")]
    public List<PasswordShare> ExistingShares { get; set; } = [];

    [Display(Name = "Available Roles")]
    public List<IdentityRole> AvailableRoles { get; set; } = [];
}
