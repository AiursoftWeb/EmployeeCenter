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
    public async Task GetIncomeCertificatePageNormalUserForbiddenTest()
    {
        await RegisterAndLoginAsync();
        var response = await Http.GetAsync("/Certificate/Income");
        AssertRedirect(response, "/Error/Code403?ReturnUrl=%2FCertificate%2FIncome");
    }
}
