using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.RequirementViewModels;

public class ReaderViewModel : UiStackLayoutViewModel
{
    public ReaderViewModel()
    {
        PageTitle = "Requirement Details";
    }

    public required Requirement Requirement { get; set; }

    public string? NewCommentContent { get; set; }

    public int? ReplyToCommentId { get; set; }
}
