using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.LedgerViewModels;

public class OcrResultsViewModel : UiStackLayoutViewModel
{
    public OcrResultsViewModel()
    {
        PageTitle = "Ocr Results";
    }

    public required Transaction Transaction { get; set; }
    public List<TransactionOcrResult> OcrResults { get; set; } = new();
}
