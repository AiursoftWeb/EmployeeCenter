using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class AudioAsrSegment
{
    public int AudioId { get; set; }

    [ForeignKey(nameof(AudioId))]
    public Audio? Audio { get; set; }

    public int SegmentIndex { get; set; }

    public long CoreStartMilliseconds { get; set; }

    public long CoreEndMilliseconds { get; set; }

    public long InputStartMilliseconds { get; set; }

    public long InputEndMilliseconds { get; set; }

    public int SegmentDurationSeconds { get; set; }

    public int OverlapSeconds { get; set; }

    public required string SegmentsJson { get; set; }

    public required string PlainText { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}
