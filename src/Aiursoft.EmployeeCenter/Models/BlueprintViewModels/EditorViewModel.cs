using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.BlueprintViewModels;

public class EditorViewModel : UiStackLayoutViewModel
{
    public EditorViewModel()
    {
        PageTitle = "Edit Blueprint";
    }
    [Display(Name = "Document Id")]
    public int? DocumentId { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(200, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Content (Markdown)")]
    public string InputMarkdown { get; set; } = string.Empty;

    [Display(Name = "Saved Successfully")]
    public bool SavedSuccessfully { get; set; }

    public bool IsEditing => DocumentId.HasValue;

    [Display(Name = "Public Link")]
    public string? PublicLink { get; set; }

    public int? FolderId { get; set; }
}
