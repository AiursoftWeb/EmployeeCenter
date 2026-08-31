using Aiursoft.EmployeeCenter.Services.DnsAudit;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class ServiceAvailabilityEvaluatorTests
{
    [TestMethod]
    public void RunningHttpServiceIsAuditedByDefault()
    {
        var service = new Service
        {
            Domain = "public.example.com",
            Protocols = "HTTPS",
            Status = ServiceStatus.Running
        };

        Assert.IsTrue(ServiceAvailabilityEvaluator.ShouldAudit(service));
    }

    [TestMethod]
    public void ExplicitlyDisabledServiceIsNotAvailabilityAudited()
    {
        var service = new Service
        {
            Domain = "restricted.example.com",
            Protocols = "HTTPS",
            Status = ServiceStatus.Running,
            IsAvailabilityAuditEnabled = false
        };

        Assert.IsFalse(ServiceAvailabilityEvaluator.ShouldAudit(service));
    }

    [TestMethod]
    [DataRow(HttpStatusCode.OK)]
    [DataRow(HttpStatusCode.Found)]
    [DataRow(HttpStatusCode.Unauthorized)]
    [DataRow(HttpStatusCode.Forbidden)]
    [DataRow(HttpStatusCode.NotFound)]
    [DataRow(HttpStatusCode.TooManyRequests)]
    public void ReachableHttpResponsesAreHealthy(HttpStatusCode statusCode)
    {
        var result = ServiceAvailabilityEvaluator.Evaluate(statusCode);

        Assert.IsTrue(result.IsHealthy);
        Assert.AreEqual((int)statusCode, result.StatusCode);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.InternalServerError)]
    [DataRow(HttpStatusCode.BadGateway)]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    [DataRow(HttpStatusCode.GatewayTimeout)]
    public void ServerErrorsAreUnhealthy(HttpStatusCode statusCode)
    {
        var result = ServiceAvailabilityEvaluator.Evaluate(statusCode);

        Assert.IsFalse(result.IsHealthy);
        Assert.AreEqual((int)statusCode, result.StatusCode);
    }

    [TestMethod]
    public async Task AvailabilityRetryStopsAfterFirstHealthyAttempt()
    {
        var attempts = 0;
        var delays = 0;

        var result = await ServiceAvailabilityRetryPolicy.ExecuteAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult(new ServiceAvailabilityResult(true, 200, "Healthy."));
            },
            (_, _) =>
            {
                delays++;
                return Task.CompletedTask;
            });

        Assert.IsTrue(result.IsHealthy);
        Assert.AreEqual(1, attempts);
        Assert.AreEqual(0, delays);
    }

    [TestMethod]
    public async Task AvailabilityRetryAcceptsAHealthyRetry()
    {
        var attempts = 0;

        var result = await ServiceAvailabilityRetryPolicy.ExecuteAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult(attempts == 3
                    ? new ServiceAvailabilityResult(true, 200, "Healthy.")
                    : new ServiceAvailabilityResult(false, null, "Timed out."));
            },
            (_, _) => Task.CompletedTask);

        Assert.IsTrue(result.IsHealthy);
        Assert.AreEqual(3, attempts);
        StringAssert.Contains(result.Details, "Succeeded on attempt 3 of 3");
    }

    [TestMethod]
    public async Task AvailabilityRetryRequiresThreeFailuresBeforeReportingFailure()
    {
        var attempts = 0;
        var delays = 0;

        var result = await ServiceAvailabilityRetryPolicy.ExecuteAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult(new ServiceAvailabilityResult(false, 503, "HTTP 503."));
            },
            (_, _) =>
            {
                delays++;
                return Task.CompletedTask;
            });

        Assert.IsFalse(result.IsHealthy);
        Assert.AreEqual(3, attempts);
        Assert.AreEqual(2, delays);
        StringAssert.Contains(result.Details, "All 3 availability attempts failed");
        StringAssert.Contains(result.Details, "HTTP 503");
    }
}
