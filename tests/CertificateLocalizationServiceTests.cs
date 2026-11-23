using Aiursoft.EmployeeCenter.Models.CertificateViewModels;
using Aiursoft.EmployeeCenter.Services;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class CertificateLocalizationServiceTests
{
    private readonly CertificateLocalizationService _service = new();
    private readonly User _testUser = new()
    {
        DisplayName = "John Doe",
        LegalName = "Johnathan Doe",
        AvatarRelativePath = User.DefaultAvatarPath,
        Title = "Senior Software Engineer",
        BaseSalary = 25000.50m,
        CreationTime = new DateTime(2023, 5, 15, 0, 0, 0, DateTimeKind.Utc)
    };

    [TestMethod]
    public void AllTwentyNineCultures_GenerateValidCertificates()
    {
        var cultures = CertificateLocalizationService.SupportedCultures;
        Assert.AreEqual(29, cultures.Count);

        foreach (var cultureKey in cultures.Keys)
        {
            // 1. Employment certificate
            var empContent = _service.BuildCertificate(
                type: CertificateType.Employment,
                requestedCulture: cultureKey,
                user: _testUser,
                companyName: "蓝鲸科技",
                companyNameEnglish: "Blue Whale Tech Co., Ltd.",
                issueDate: new DateTime(2026, 11, 23));

            Assert.IsFalse(string.IsNullOrWhiteSpace(empContent.Title), $"Title empty for {cultureKey}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(empContent.Paragraph1Html), $"P1 empty for {cultureKey}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(empContent.Paragraph2Html), $"P2 empty for {cultureKey}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(empContent.DisplayedCompanyName), $"Company empty for {cultureKey}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(empContent.FormattedIssueDate), $"Date empty for {cultureKey}");
            StringAssert.Contains(empContent.Paragraph1Html, _testUser.LegalName);
            if (!cultureKey.StartsWith("zh"))
            {
                StringAssert.Contains(empContent.Paragraph1Html, empContent.DisplayedCompanyName);
            }

            // 2. Income certificate
            var incContent = _service.BuildCertificate(
                type: CertificateType.Income,
                requestedCulture: cultureKey,
                user: _testUser,
                companyName: "蓝鲸科技",
                companyNameEnglish: "Blue Whale Tech Co., Ltd.",
                issueDate: new DateTime(2026, 11, 23));

            Assert.IsFalse(string.IsNullOrWhiteSpace(incContent.Title), $"Income Title empty for {cultureKey}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(incContent.Paragraph1Html), $"Income P1 empty for {cultureKey}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(incContent.Paragraph2Html), $"Income P2 empty for {cultureKey}");
            StringAssert.Contains(incContent.Paragraph1Html, _testUser.LegalName);
            if (!cultureKey.StartsWith("zh"))
            {
                StringAssert.Contains(incContent.Paragraph1Html, incContent.DisplayedCompanyName);
            }
            var expectedSalaryStr = _testUser.BaseSalary.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo(cultureKey));
            StringAssert.Contains(incContent.Paragraph1Html, expectedSalaryStr);
        }
    }

    [TestMethod]
    [DataRow("zh-CN", "在职证明", "特此证明。")]
    [DataRow("zh-TW", "在職證明", "特此證明。")]
    [DataRow("zh-HK", "在職證明", "特此證明。")]
    [DataRow("en-US", "Certificate of Employment", "This certificate is issued at the request of the employee.")]
    [DataRow("en-GB", "Certificate of Employment", "This certificate is issued at the request of the employee.")]
    [DataRow("hr-HR", "Potvrda o zaposlenju", "Potvrda se izdaje na zahtjev zaposlenika.")]
    [DataRow("ja-JP", "在職証明書", "本証明書は本人の請求に基づき発行されたものです。")]
    [DataRow("ko-KR", "재직증명서", "본 증명서는 직원의 요청에 따라 발급되었습니다.")]
    [DataRow("de-DE", "Arbeitsbescheinigung", "Diese Bescheinigung wird auf Antrag des Arbeitnehmers ausgestellt.")]
    [DataRow("fr-FR", "Certificat de travail", "Cette attestation est délivrée à la demande de l'employé(e).")]
    public void SpecificCultures_MatchKnownPhrases(string culture, string expectedTitle, string expectedP2)
    {
        var content = _service.BuildCertificate(
            type: CertificateType.Employment,
            requestedCulture: culture,
            user: _testUser,
            companyName: "蓝鲸科技",
            companyNameEnglish: "Blue Whale Tech Co., Ltd.",
            issueDate: new DateTime(2026, 11, 23));

        Assert.AreEqual(expectedTitle, content.Title);
        Assert.AreEqual(expectedP2, content.Paragraph2Html);
    }
}
