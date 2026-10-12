using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.PasswordsViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "Passwords Management";
    }

    [Display(Name = "My Passwords")]
    public List<Password> MyPasswords { get; set; } = [];

    [Display(Name = "Shared With Me")]
    public List<PasswordShare> SharedWithMe { get; set; } = [];
}
