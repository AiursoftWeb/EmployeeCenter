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
    public string? MeetingMinutesMarkdown { get; set; }
    public int MeetingMinutesAttemptCount { get; set; }
    public DateTime? LastMeetingMinutesAttemptTime { get; set; }

    public bool MeetingMinutesOutdated { get; set; }

    public bool CanManageShares { get; set; }

    public SharePermission Permission { get; set; }
}
