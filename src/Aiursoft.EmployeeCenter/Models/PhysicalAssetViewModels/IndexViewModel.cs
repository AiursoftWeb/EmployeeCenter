using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.PhysicalAssetViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "Physical Assets";
    }

    public List<PhysicalAsset> Assets { get; set; } = [];
    public List<PhysicalAssetUsage> MyUsages { get; set; } = [];
}
