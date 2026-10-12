using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.CustomerRelationshipViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "Customer Relationships";
    }

    [Display(Name = "Customer Relationships")]
    public IEnumerable<CustomerRelationship> CustomerRelationships { get; set; } = [];
}
