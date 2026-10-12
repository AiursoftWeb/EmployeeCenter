using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;

namespace Aiursoft.EmployeeCenter.Models.CollectionChannelsViewModels;

public class EditViewModel : CreateViewModel
{
    public EditViewModel()
    {
        PageTitle = "Edit";
    }

    public int Id { get; set; }

    [Display(Name = "Status")]
    public CollectionChannelStatus Status { get; set; }
}
