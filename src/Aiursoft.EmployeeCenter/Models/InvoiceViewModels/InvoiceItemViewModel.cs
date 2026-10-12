using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Models.InvoiceViewModels;

public class InvoiceItemViewModel
{

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Quantity")]
    public decimal Quantity { get; set; } = 1;

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Unit Price")]
    public decimal UnitPrice { get; set; }
}
