using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.MarketChannelsViewModels;

public class DetailsViewModel : UiStackLayoutViewModel
{
    public DetailsViewModel()
    {
        PageTitle = "Details";
    }

    public required MarketChannel MarketChannel { get; init; }
    public string? RenderedDescription { get; init; }
}
