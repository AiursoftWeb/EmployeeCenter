using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.AudioViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "Meeting Transcripts";
    }

    public List<AudioListItemViewModel> Audios { get; set; } = new();

    public int TotalAudioCount { get; set; }

    public int Page { get; set; }

    public bool HasNextPage { get; set; }
}

public class AudioListItemViewModel
{
    public required Audio Audio { get; set; }
    public bool HasTranscript { get; set; }
    public bool IsEmptyResult { get; set; }
    public bool HasMeetingMinutes { get; set; }
    public int MeetingMinutesAttemptCount { get; set; }
}
