using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Aiursoft.EmployeeCenter.Models.QualificationsViewModels;

public sealed class PrintableSingleLineAttribute() : ValidationAttribute("Enter valid single-line text without control characters.")
{
    public override bool IsValid(object? value) => value is string text &&
        !string.IsNullOrWhiteSpace(text) &&
        !text.Any(character => char.IsControl(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format);
}
