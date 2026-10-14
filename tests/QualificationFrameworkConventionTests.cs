using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Aiursoft.EmployeeCenter.Models.QualificationsViewModels;
using Aiursoft.UiStack.Layout;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class QualificationFrameworkConventionTests
{
    [TestMethod]
    [DataRow(typeof(BusinessQualification), nameof(BusinessQualification.CompanyEntityId), nameof(BusinessQualification.CompanyEntity))]
    [DataRow(typeof(QualificationDownload), nameof(QualificationDownload.BusinessQualificationId), nameof(QualificationDownload.BusinessQualification))]
    [DataRow(typeof(QualificationLinkIssue), nameof(QualificationLinkIssue.QualificationDownloadId), nameof(QualificationLinkIssue.QualificationDownload))]
    public void EntitiesDeclareImmutableKeysAndExplicitRequiredRelationships(Type type, string foreignKey, string navigation)
    {
        var key = type.GetProperty("Id")!;
        Assert.IsNotNull(key.GetCustomAttribute<KeyAttribute>());
        Assert.IsTrue(key.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()
            .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)));
        var reference = type.GetProperty(navigation)!;
        Assert.IsNotNull(reference.GetCustomAttribute<JsonIgnoreAttribute>());
        Assert.IsNotNull(reference.GetMethod!.ReturnParameter
            .GetCustomAttribute<NotNullAttribute>());
        Assert.AreEqual(foreignKey, reference.GetCustomAttribute<ForeignKeyAttribute>()!.Name);
        Assert.IsNotNull(type.GetProperty(foreignKey)!.GetCustomAttribute<System.Runtime.CompilerServices.RequiredMemberAttribute>());
        Assert.IsFalse(reference.GetMethod!.IsVirtual);
        foreach (var property in type.GetProperties().Where(p => p.PropertyType == typeof(string)))
            Assert.IsNotNull(property.GetCustomAttribute<MaxLengthAttribute>(), property.Name);
    }

    [TestMethod]
    public void FormModelsNormalizeTextBeforeApplyingBusinessValidation()
    {
        var model = new ApplyViewModel { Purpose = "   ", Recipient = "  AB  " };
        var errors = new List<ValidationResult>();
        Assert.AreEqual(string.Empty, model.Purpose);
        Assert.AreEqual("AB", model.Recipient);
        Assert.IsFalse(Validator.TryValidateObject(model, new ValidationContext(model), errors, validateAllProperties: true));
        model.Purpose = "Supplier onboarding\u200b";
        Assert.IsFalse(new PrintableSingleLineAttribute().IsValid(model.Purpose));
        model.Purpose = "  Supplier onboarding  ";
        errors.Clear();
        Assert.IsTrue(Validator.TryValidateObject(model, new ValidationContext(model), errors, validateAllProperties: true));
    }

    [TestMethod]
    [DataRow("auto", true)]
    [DataRow("zh-CN", true)]
    [DataRow("zh-HK", true)]
    [DataRow("en-US", true)]
    [DataRow("fr-FR", false)]
    [DataRow("en", false)]
    [DataRow("", false)]
    [DataRow(null, false)]
    public void WatermarkLanguageDefaultsToInterfaceAndRejectsUnsupportedChoices(string? language, bool valid)
    {
        var model = new ApplyViewModel { Purpose = "Supplier onboarding", Recipient = "Test recipient" };
        Assert.AreEqual("auto", model.WatermarkLanguage);
        model.WatermarkLanguage = language!;
        var errors = new List<ValidationResult>();
        Assert.AreEqual(valid, Validator.TryValidateObject(model, new ValidationContext(model), errors, validateAllProperties: true));
    }

    [TestMethod]
    public void PageModelsInitializeLayoutTitlesAndBindLogicalPathsInsteadOfFiles()
    {
        foreach (var model in new UiStackLayoutViewModel[]
        {
            new IndexViewModel(), new ManageIndexViewModel(), new CreateViewModel(), new ApplyViewModel(),
            new RecordsViewModel(), new ManageRecordsViewModel(), new DetectViewModel(),
            new PreviewViewModel { Record = null! }, new ManageRecordViewModel { Record = null! }
        })
            Assert.IsFalse(string.IsNullOrWhiteSpace(model.PageTitle), model.GetType().Name);
        Assert.AreEqual(typeof(string), typeof(CreateViewModel).GetProperty(nameof(CreateViewModel.OriginalPath))!.PropertyType);
        Assert.AreEqual(typeof(string), typeof(DetectViewModel).GetProperty(nameof(DetectViewModel.ImagePath))!.PropertyType);
        Assert.IsFalse(typeof(CreateViewModel).GetProperties().Concat(typeof(DetectViewModel).GetProperties())
            .Any(p => typeof(IFormFile).IsAssignableFrom(p.PropertyType)));
    }
}
