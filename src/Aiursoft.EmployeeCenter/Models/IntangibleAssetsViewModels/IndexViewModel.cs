using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.IntangibleAssetsViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "Index";
    }

    [Display(Name = "Assets")]
    public IEnumerable<IntangibleAsset> Assets { get; set; } = new List<IntangibleAsset>();

    [Display(Name = "My Assets")]
    public IEnumerable<IntangibleAsset> MyAssets { get; set; } = new List<IntangibleAsset>();
}
