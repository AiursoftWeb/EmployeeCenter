using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Models.ServicesViewModels;

public class EditServiceViewModel : CreateServiceViewModel
{
    public EditServiceViewModel()
    {
        PageTitle = "Edit Service";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    public string? ConcurrencyToken { get; set; }
}
