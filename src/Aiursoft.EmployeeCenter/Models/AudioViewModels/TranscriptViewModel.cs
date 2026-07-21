using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.AudioViewModels;

public class TranscriptViewModel : UiStackLayoutViewModel
{
    public TranscriptViewModel()
    {
        PageTitle = "Meeting Transcript";
    }

    public required Audio Audio { get; set; }
    public string? PlainText { get; set; }
}
