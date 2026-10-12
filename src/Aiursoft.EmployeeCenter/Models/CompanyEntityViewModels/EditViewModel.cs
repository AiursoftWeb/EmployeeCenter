using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Models.CompanyEntityViewModels;

public class EditViewModel : CreateViewModel
{
    public EditViewModel()
    {
        PageTitle = "Edit Company Entity";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Id")]
    public int Id { get; set; }
}
