using System.Reflection;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.BackgroundJobs;
using Aiursoft.EmployeeCenter.Services.FileStorage;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class MoreEmployeeCenterCoverageTests : TestBase
{
    [TestMethod]
    public void TestPathResolverUnit()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ec_pr_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:Path"] = tempDir
                })
                .Build();

            var resolver = new PathResolver(config);
            var workspaceFile = Path.Combine(tempDir, "Workspace", "subfolder", "sample.png");

            var cleared = resolver.GetClearedExifAbsolutePath(workspaceFile);
            Assert.IsTrue(cleared.Contains("ClearExif"));
            Assert.IsTrue(cleared.EndsWith("sample.png"));

            var compressedWh = resolver.GetCompressedAbsolutePath(workspaceFile, 800, 600);
            Assert.IsTrue(compressedWh.Contains("Compressed"));
            Assert.IsTrue(compressedWh.Contains("_w800_h600"));

            var compressedW = resolver.GetCompressedAbsolutePath(workspaceFile, 800, 0);
            Assert.IsTrue(compressedW.Contains("_w800"));

            var compressedH = resolver.GetCompressedAbsolutePath(workspaceFile, 0, 600);
            Assert.IsTrue(compressedH.Contains("_h600"));

            var compressedNone = resolver.GetCompressedAbsolutePath(workspaceFile, 0, 0);
            Assert.IsFalse(compressedNone.Contains("_w"));
            Assert.IsFalse(compressedNone.Contains("_h"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [TestMethod]
    public async Task TestViewModelArgsInjectorAndAdditionalPages()
    {
        await LoginAsAdmin();

        using (var scope = Server!.Services.CreateScope())
        {
            var injector = scope.ServiceProvider.GetRequiredService<ViewModelArgsInjector>();
            var method = typeof(ViewModelArgsInjector).GetMethod("_useless_for_localizer", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(injector, null);

            var leaveJob = scope.ServiceProvider.GetRequiredService<AnnualLeaveAllocationJob>();
            await leaveJob.ExecuteAsync();
        }

        var pagesToVisit = new[]
        {
            "/CompanyEntity",
            "/CompanyEntity/Create",
            "/Users",
            "/Roles",
            "/CollectionChannels",
            "/CustomerRelationships",
            "/ManagePayroll",
            "/Reimbursement",
            "/Blueprint",
            "/IntangibleAssets",
            "/MarketChannels",
            "/Leave",
            "/Servers",
            "/Services",
            "/Qualifications"
        };

        foreach (var page in pagesToVisit)
        {
            var response = await Http.GetAsync(page);
            Assert.IsTrue(response.IsSuccessStatusCode, $"Failed GET on {page} with status {response.StatusCode}");
        }
    }
}
