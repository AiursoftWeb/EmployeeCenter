using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.AudioViewModels;

public class EditTranscriptViewModel : UiStackLayoutViewModel
{
    public EditTranscriptViewModel()
    {
        PageTitle = "Edit Transcript";
    }

    [Required]
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Transcribed Text")]
    public string PlainText { get; set; } = string.Empty;
}
