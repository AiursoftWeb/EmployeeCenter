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
    [DataRow("Employment", "Potvrda o zaposlenju", false)]
    [DataRow("Income", "Potvrda o primanjima", true)]
    public async Task CanPrintCroatianCertificate(string type, string title, bool hasSalary)
    {
        await LoginAsAdmin();
        var page = await Http.GetAsync($"/Certificate/{type}");
        Assert.Contains("value=\"hr-HR\"", await page.Content.ReadAsStringAsync());

        var printed = await PostForm("/Certificate/Print", new()
        {
            ["type"] = type,
            ["lang"] = "hr-HR"
        }, tokenUrl: $"/Certificate/{type}");
        Assert.AreEqual(HttpStatusCode.OK, printed.StatusCode);

        var html = WebUtility.HtmlDecode(await printed.Content.ReadAsStringAsync());
        Assert.Contains("<html lang=\"hr-HR\">", html);
        Assert.Contains(title, html);
        Assert.AreEqual(hasSalary, html.Contains("Mjesečna plaća", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetIncomeCertificatePageNormalUserForbiddenTest()
    {
        await RegisterAndLoginAsync();
        var response = await Http.GetAsync("/Certificate/Income");
        AssertRedirect(response, "/Error/Code403?ReturnUrl=%2FCertificate%2FIncome");
    }
}
