using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.PasswordsViewModels;

public class DetailsViewModel : UiStackLayoutViewModel
{
    public DetailsViewModel()
    {
        PageTitle = "Password Details";
    }

    [Display(Name = "Password")]
    public required Password Password { get; set; }

    [Display(Name = "Permission")]
    public SharePermission Permission { get; set; }
}
