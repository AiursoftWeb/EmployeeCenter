using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Models.CollectionRecordsViewModels;

public class EditViewModel : CreateViewModel
{
    public EditViewModel()
    {
        PageTitle = "Edit";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    public int Id { get; set; }
}
