using Aiursoft.EmployeeCenter.Services.FileStorage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Aiursoft.EmployeeCenter.Views.Shared.Components.FileUpload;

public class FileUpload(StorageService storageService) : ViewComponent
{
    public IViewComponentResult Invoke(
        ModelExpression aspFor,
        string subfolder,
        int maxSizeInMb = 2000,
        string? allowedExtensions = null,
        bool isVault = false,
        string? fieldName = null,
        string? uploadEndpoint = null)
    {
        return View(new FileUploadViewModel
        {
            AspFor = aspFor,
            UploadEndpoint = uploadEndpoint ?? storageService.GetUploadUrl(subfolder, isVault),
            MaxSizeInMb = maxSizeInMb,
            AllowedExtensions = allowedExtensions,
            IsVault = isVault,
            FieldName = fieldName
        });
    }
}
