using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ContractViewModels;

public class OcrPreviewViewModel: UiStackLayoutViewModel
{
    public OcrPreviewViewModel()
    {
        PageTitle = "Preview";
    }

    public required Contract Contract { get; set; }
    public List<ContractChangeLog> History { get; set; } = [];
    public string? UploadedBy { get; set; }
    public string? LastModifiedBy { get; set; }
    public bool CanViewHistory { get; set; }
    public string? PlainText { get; set; }
    public string? JsonResult { get; set; }
}
