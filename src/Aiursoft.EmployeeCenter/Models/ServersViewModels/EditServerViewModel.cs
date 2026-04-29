using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Models.ServersViewModels;

public class EditServerViewModel : CreateServerViewModel
{
    public EditServerViewModel()
    {
        PageTitle = "Edit Server";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Id")]
    public int Id { get; set; }
}
