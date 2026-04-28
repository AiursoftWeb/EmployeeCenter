using Newtonsoft.Json.Linq;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class DropdownApiTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public DropdownApiTests()
    {
        var cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            AllowAutoRedirect = false
        };
        _port = Network.GetAvailablePort();
        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri($"http://localhost:{_port}")
        };
    }

    [TestInitialize]
    public async Task CreateServer()
    {
        _server = await AppAsync<Startup>([], port: _port);
        await _server.UpdateDbAsync<EmployeeCenterDbContext>();
        await _server.SeedAsync();
        await _server.StartAsync();

        // Login as admin
        var response = await _http.GetAsync("/Account/Login");
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, @"__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        var loginToken = match.Groups[1].Value;

        var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", "admin" },
            { "Password", "Admin@123456!" },
            { "__RequestVerificationToken", loginToken }
        });
        await _http.PostAsync("/Account/Login", loginContent);
    }

    [TestCleanup]
    public async Task CleanServer()
    {
        if (_server == null) return;
        await _server.StopAsync();
        _server.Dispose();
    }

    private async Task VerifyApi(string url, string[] expectedKeys)
    {
        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var array = JArray.Parse(json);

        if (array.Count > 0)
        {
            var firstItem = (JObject)array[0];
            foreach (var key in expectedKeys)
            {
                Assert.IsNotNull(firstItem[key], $"API {url} is missing expected key: {key}. JSON: {json}");
            }
        }
    }

    [TestMethod]
    public async Task TestAssetsDropdownApis()
    {
        await VerifyApi("/Assets/GetModels", new[] { "Id", "ModelName", "CategoryName" });
        await VerifyApi("/Assets/GetLocations", new[] { "Id", "Name" });
        await VerifyApi("/Assets/GetCompanyEntities", new[] { "Id", "CompanyName" });
        await VerifyApi("/Assets/GetVendors", new[] { "Id", "Name" });
        await VerifyApi("/Assets/GetUsers", new[] { "Id", "DisplayName" });
        await VerifyApi("/Assets/GetCategories", new[] { "Id", "Name" });
    }

    [TestMethod]
    public async Task TestServersDropdownApis()
    {
        await VerifyApi("/Servers/GetLocations", new[] { "Id", "Name" });
        await VerifyApi("/Servers/GetProviders", new[] { "Id", "Name" });
        await VerifyApi("/Servers/GetUsers", new[] { "Id", "DisplayName" });
    }

    [TestMethod]
    public async Task TestServicesDropdownApis()
    {
        await VerifyApi("/Services/GetLocations", new[] { "Id", "Name" });
        await VerifyApi("/Services/GetProviders", new[] { "Id", "Name" });
        await VerifyApi("/Services/GetCompanyEntities", new[] { "Id", "CompanyName" });
        await VerifyApi("/Services/GetDnsProviders", new[] { "Id", "Domain" });
        await VerifyApi("/Services/GetServers", new[] { "Id", "HostName" });
    }
}
