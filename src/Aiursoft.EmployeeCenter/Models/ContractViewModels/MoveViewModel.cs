using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ContractViewModels;

public class MoveViewModel : UiStackLayoutViewModel
{
    public MoveViewModel()
    {
        PageTitle = "Move Contract";
    }

    public int ContractId { get; set; }

    public string ContractName { get; set; } = string.Empty;

    /// <summary>
    /// The folder currently being browsed. Null means the root level.
    /// </summary>
    public int? BrowseFolderId { get; set; }

    /// <summary>
    /// The currently browsed folder, used for breadcrumb display.
    /// </summary>
    public ContractFolder? BrowseFolder { get; set; }

    /// <summary>
    /// Direct subfolders at the current browse level.
    /// </summary>
    public List<ContractFolder> SubFolders { get; set; } = [];

    /// <summary>
    /// Breadcrumb path from the root to the parent of the browsed folder.
    /// </summary>
    public List<ContractFolder> Breadcrumb { get; set; } = [];
}
