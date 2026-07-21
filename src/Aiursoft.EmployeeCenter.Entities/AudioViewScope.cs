namespace Aiursoft.EmployeeCenter.Entities;

/// <summary>
/// The visibility scope of a meeting recording.
/// </summary>
public enum AudioViewScope
{
    /// <summary>Only the uploader and administrators can view.</summary>
    Private = 0,

    /// <summary>Anyone in the same department as the uploader can view.</summary>
    Department = 1,

    /// <summary>Any user with the CanViewAudio permission can view.</summary>
    Public = 2
}
