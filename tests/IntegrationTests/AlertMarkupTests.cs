using AngleSharp.Html.Parser;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class AlertMarkupTests : TestBase
{
    [TestMethod]
    [DataRow("/Qualifications")]
    [DataRow("/ManageQualifications")]
    [DataRow("/ManageQualifications/Create")]
    [DataRow("/ManageQualifications/Detect")]
    [DataRow("/Services/DataQuality")]
    [DataRow("/Home/SelfHost")]
    public async Task RenderedNoticesHaveAccessibleDismissButtonIconAndMessage(string path)
    {
        await LoginAsAdmin();
        var response = await Http.GetAsync(path);
        response.EnsureSuccessStatusCode();
        using var document = new HtmlParser().ParseDocument(await response.Content.ReadAsStringAsync());
        var alerts = document.QuerySelectorAll(".alert");
        Assert.IsTrue(alerts.Length > 0, $"No notices rendered on {path}.");
        foreach (var alert in alerts)
        {
            Assert.AreEqual("alert", alert.GetAttribute("role"));
            Assert.IsTrue(alert.ClassList.Contains("alert-dismissible"));
            Assert.IsTrue(alert.ParentElement!.ClassList.Contains("mb-3"));
            var icon = alert.Children.Single(c => c.ClassList.Contains("alert-icon"));
            Assert.IsNotNull(icon.QuerySelector(".fa-bell"));
            var message = alert.Children.Single(c => c.ClassList.Contains("alert-message"));
            Assert.IsFalse(string.IsNullOrWhiteSpace(message.TextContent));
            var close = alert.Children.Single(c => c.ClassList.Contains("btn-close"));
            Assert.AreEqual("button", close.GetAttribute("type"));
            Assert.AreEqual("alert", close.GetAttribute("data-bs-dismiss"));
            Assert.IsFalse(string.IsNullOrWhiteSpace(close.GetAttribute("aria-label")));
        }
    }
}
