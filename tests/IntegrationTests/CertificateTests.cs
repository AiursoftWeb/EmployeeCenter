using Aiursoft.EmployeeCenter.Services;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class CertificateTests : TestBase
{
    [TestMethod]
    public async Task GetEmploymentCertificatePageRedirectsAnonymousTest()
    {
        var response = await Http.GetAsync("/Certificate/Employment");
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("Account/Login", response.Headers.Location!.ToString());
    }

    [TestMethod]
    public async Task GetEmploymentCertificatePageAdminAccessTest()
    {
        await LoginAsAdmin();
        var response = await Http.GetAsync("/Certificate/Employment");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetEmploymentCertificatePageNormalUserForbiddenTest()
    {
        await RegisterAndLoginAsync();
        var response = await Http.GetAsync("/Certificate/Employment");
        AssertRedirect(response, "/Error/Code403?ReturnUrl=%2FCertificate%2FEmployment");
    }

    [TestMethod]
    public async Task GetIncomeCertificatePageAdminAccessTest()
    {
        await LoginAsAdmin();
        var response = await Http.GetAsync("/Certificate/Income");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task CertificatePage_ContainsAllTwentyNineLanguageOptions()
    {
        await LoginAsAdmin();
        var page = await Http.GetAsync("/Certificate/Employment");
        var content = await page.Content.ReadAsStringAsync();

        foreach (var cultureKey in CertificateLocalizationService.SupportedCultures.Keys)
        {
            Assert.Contains($"value=\"{cultureKey}\"", content);
        }
    }

    [TestMethod]
    [DataRow("Employment", "hr-HR", "Potvrda o zaposlenju", false, "Mjesečna plaća")]
    [DataRow("Income", "hr-HR", "Potvrda o primanjima", true, "Mjesečna plaća")]
    [DataRow("Employment", "zh-CN", "在职证明", false, "其月薪为")]
    [DataRow("Income", "zh-CN", "收入证明", true, "其月薪为")]
    [DataRow("Employment", "en-US", "Certificate of Employment", false, "monthly salary")]
    [DataRow("Income", "en-US", "Certificate of Income", true, "monthly salary")]
    [DataRow("Employment", "ja-JP", "在職証明書", false, "月給")]
    [DataRow("Income", "ja-JP", "給与証明書", true, "月給")]
    public async Task CanPrintCertificateInDifferentLanguages(
        string type,
        string lang,
        string expectedTitle,
        bool hasSalary,
        string salaryKeyword)
    {
        await LoginAsAdmin();
        var printed = await PostForm("/Certificate/Print", new()
        {
            ["type"] = type,
            ["lang"] = lang
        }, tokenUrl: $"/Certificate/{type}");
        Assert.AreEqual(HttpStatusCode.OK, printed.StatusCode);

        var html = WebUtility.HtmlDecode(await printed.Content.ReadAsStringAsync());
        Assert.Contains($"<html lang=\"{lang}\"", html);
        Assert.Contains(expectedTitle, html);
        Assert.AreEqual(hasSalary, html.Contains(salaryKeyword, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetIncomeCertificatePageNormalUserForbiddenTest()
    {
        await RegisterAndLoginAsync();
        var response = await Http.GetAsync("/Certificate/Income");
        AssertRedirect(response, "/Error/Code403?ReturnUrl=%2FCertificate%2FIncome");
    }
}
