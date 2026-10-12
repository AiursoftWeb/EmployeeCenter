using Aiursoft.EmployeeCenter.Services.DnsAudit;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class PublicDnsResolverTests
{
    [TestMethod]
    public async Task ResolveAsyncReturnsOnlyPublicAddressesAndPrefersIpv4()
    {
        var resolver = CreateResolver(request =>
        {
            var type = request.RequestUri!.Query.Contains("type=1", StringComparison.Ordinal)
                ? 1
                : 28;
            var body = type == 1
                ? """{"Status":0,"Answer":[{"type":1,"data":"10.234.0.2"},{"type":1,"data":"104.21.87.193"}]}"""
                : """{"Status":0,"Answer":[{"type":28,"data":"fd00::1"},{"type":28,"data":"2606:4700:3034::ac43:91b4"}]}""";
            return JsonResponse(body);
        });

        var addresses = await resolver.ResolveAsync("aimer.aiursoft.com");

        CollectionAssert.AreEqual(
            new[] { "104.21.87.193", "2606:4700:3034::ac43:91b4" },
            addresses.Select(address => address.ToString()).ToArray());
    }

    [TestMethod]
    public async Task ResolveAsyncFallsBackToGoogleWhenCloudflareFails()
    {
        var requestedHosts = new System.Collections.Concurrent.ConcurrentBag<string>();
        var resolver = CreateResolver(request =>
        {
            requestedHosts.Add(request.RequestUri!.Host);
            if (request.RequestUri.Host == "cloudflare-dns.com")
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            var type = request.RequestUri.Query.Contains("type=1", StringComparison.Ordinal) ? 1 : 28;
            return JsonResponse(type == 1
                ? """{"Status":0,"Answer":[{"type":1,"data":"8.8.8.8"}]}"""
                : """{"Status":0}""");
        });

        var addresses = await resolver.ResolveAsync("example.com");

        Assert.AreEqual(1, addresses.Count);
        Assert.AreEqual("8.8.8.8", addresses[0].ToString());
        Assert.AreEqual(2, requestedHosts.Count(host => host == "cloudflare-dns.com"));
        Assert.AreEqual(2, requestedHosts.Count(host => host == "dns.google"));
    }

    [TestMethod]
    [DataRow("127.0.0.1", false)]
    [DataRow("10.234.0.2", false)]
    [DataRow("100.64.0.1", false)]
    [DataRow("169.254.169.254", false)]
    [DataRow("172.16.0.1", false)]
    [DataRow("192.168.1.1", false)]
    [DataRow("203.0.113.10", false)]
    [DataRow("2001:db8::1", false)]
    [DataRow("fd00::1", false)]
    [DataRow("fe80::1", false)]
    [DataRow("104.21.87.193", true)]
    [DataRow("2606:4700:3034::ac43:91b4", true)]
    public void PublicAddressPolicyRejectsNonPublicNetworks(string address, bool expected)
    {
        Assert.AreEqual(expected, PublicDnsResolver.IsPublicAddress(IPAddress.Parse(address)));
    }

    private static PublicDnsResolver CreateResolver(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var client = new HttpClient(new StubHandler(responder));
        return new PublicDnsResolver(new StubHttpClientFactory(client));
    }

    private static HttpResponseMessage JsonResponse(string body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/dns-json")
        };
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
