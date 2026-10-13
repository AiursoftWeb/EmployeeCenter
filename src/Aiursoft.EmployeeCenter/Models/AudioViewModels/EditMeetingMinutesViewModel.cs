using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.AudioViewModels;

public class EditMeetingMinutesViewModel : UiStackLayoutViewModel
{
    public EditMeetingMinutesViewModel()
    {
        PageTitle = "Edit Meeting Minutes";
    }

    public int Id { get; set; }
    public int TranscriptRevision { get; set; }

    [Range(1, long.MaxValue)]
    public long TranscriptCreateTimeTicks { get; set; }

    [Required]
    public string OriginalMeetingMinutesHash { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Meeting Minutes")]
    public string MeetingMinutesMarkdown { get; set; } = string.Empty;
}
