using Aiursoft.EmployeeCenter.Services.DnsAudit;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class ServiceAvailabilityEvaluatorTests
{
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
}
