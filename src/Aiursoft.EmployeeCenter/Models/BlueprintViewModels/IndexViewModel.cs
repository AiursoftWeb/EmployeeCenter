using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.BlueprintViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "Blueprints";
    }

    public int? FolderId { get; set; }

    public BlueprintFolder? CurrentFolder { get; set; }

    [Display(Name = "Sub Folders")]
    public IEnumerable<BlueprintFolder> SubFolders { get; set; } = new List<BlueprintFolder>();

    [Display(Name = "Blueprints")]
    public IEnumerable<Blueprint> Blueprints { get; set; } = new List<Blueprint>();
}
