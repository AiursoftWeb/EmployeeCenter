using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Aiursoft.EmployeeCenter.Views.Shared.Components.FileUpload;

public class FileUploadViewModel
{
    public required ModelExpression AspFor { get; init; }
    public required string UploadEndpoint { get; init; }
    public required int MaxSizeInMb { get; init; }
    public required string? AllowedExtensions { get; init; }
    public bool IsVault { get; init; }
    public bool ExistingFileIsVault { get; init; }
    public bool ShowExistingFile { get; init; } = true;
    public string? FieldName { get; init; } // Optional: override the name attribute
    public string UniqueId { get; } = "uploader-" + Guid.NewGuid().ToString("N");
}
