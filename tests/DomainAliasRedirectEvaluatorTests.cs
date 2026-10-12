using Aiursoft.EmployeeCenter.Services.DnsAudit;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class DomainAliasRedirectEvaluatorTests
{
    [TestMethod]
    public void AcceptsAnExactPermanentRedirect()
    {
        var result = DomainAliasRedirectEvaluator.Evaluate(
            new Uri("https://alias.example.com/"),
            "https://target.example.com/",
            HttpStatusCode.MovedPermanently,
            new Uri("https://target.example.com/"));

        Assert.IsTrue(result.IsMatch);
        Assert.AreEqual(301, result.StatusCode);
    }

    [TestMethod]
    public void AcceptsStandardTemporaryAndPermanentRedirectCodes()
    {
        foreach (var status in new[]
                 {
                     HttpStatusCode.Found,
                     HttpStatusCode.SeeOther,
                     HttpStatusCode.TemporaryRedirect,
                     HttpStatusCode.PermanentRedirect
                 })
        {
            var result = DomainAliasRedirectEvaluator.Evaluate(
                new Uri("https://alias.example.com/"),
                "https://target.example.com/",
                status,
                new Uri("https://target.example.com/"));
            Assert.IsTrue(result.IsMatch, $"HTTP {(int)status} should be accepted.");
        }
    }

    [TestMethod]
    public void RejectsAHealthyPageThatDoesNotRedirect()
    {
        var result = DomainAliasRedirectEvaluator.Evaluate(
            new Uri("https://alias.example.com/"),
            "https://target.example.com/",
            HttpStatusCode.OK,
            null);

        Assert.IsFalse(result.IsMatch);
        Assert.Contains("HTTP 200", result.Details);
    }

    [TestMethod]
    public void RejectsAnImpreciseRedirectTarget()
    {
        var result = DomainAliasRedirectEvaluator.Evaluate(
            new Uri("https://alias.example.com/"),
            "https://target.example.com/exact",
            HttpStatusCode.PermanentRedirect,
            new Uri("https://target.example.com/other"));

        Assert.IsFalse(result.IsMatch);
        Assert.AreEqual("https://target.example.com/other", result.ActualTargetUrl);
        Assert.Contains("expects exactly", result.Details);
    }

    [TestMethod]
    public void ResolvesRelativeLocationsBeforeComparing()
    {
        var result = DomainAliasRedirectEvaluator.Evaluate(
            new Uri("https://alias.example.com/"),
            "https://alias.example.com/destination",
            HttpStatusCode.Found,
            new Uri("/destination", UriKind.Relative));

        Assert.IsTrue(result.IsMatch);
    }

    [TestMethod]
    public void TargetUrlMustBeHttpsAndCredentialFree()
    {
        Assert.IsFalse(DomainAliasRedirectEvaluator.TryNormalizeTargetUrl(
            "http://target.example.com/", out _, out _));
        Assert.IsFalse(DomainAliasRedirectEvaluator.TryNormalizeTargetUrl(
            "https://user:password@target.example.com/", out _, out _));
    }
}
