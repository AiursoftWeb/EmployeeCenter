using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.PromotionHistoryViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "Promotion History";
    }

    [Display(Name = "Total Promotions This Year")]
    public int TotalPromotionsThisYear { get; set; }

    [Display(Name = "My Promotion Count")]
    public int MyPromotionCount { get; set; }

    [Display(Name = "Time Since Last Promotion")]
    public TimeSpan TimeSinceLastPromotion { get; set; }

    [Display(Name = "Has Never Been Promoted")]
    public bool HasNeverBeenPromoted { get; set; }

    [Display(Name = "Histories")]
    public IEnumerable<PromotionHistory> Histories { get; set; } = new List<PromotionHistory>();
}
