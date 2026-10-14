using System.Net.Http.Headers;
using System.IO.Compression;
using System.Security.Cryptography;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using SkiaSharp;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class QualificationsTests : TestBase
{
    private readonly List<string> savedFiles = [];
    private readonly List<int> companyIds = [];
    private int qualificationId;

    [TestCleanup]
    public override async Task CleanServer()
    {
        foreach (var path in savedFiles) File.Delete(path);
        if (Server is not null)
        {
            using var scope = Server.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var records = await db.QualificationDownloads.Where(d => d.BusinessQualificationId == qualificationId).ToListAsync();
            foreach (var record in records)
                scope.ServiceProvider.GetRequiredService<QualificationFileService>().DeleteCopy(record);
            db.QualificationLinkIssues.RemoveRange(await db.QualificationLinkIssues
                .Where(i => records.Select(r => r.Id).Contains(i.QualificationDownloadId)).ToListAsync());
            db.QualificationDownloads.RemoveRange(records);
            db.BusinessQualifications.RemoveRange(await db.BusinessQualifications.Where(q => companyIds.Contains(q.CompanyEntityId)).ToListAsync());
            db.CompanyEntities.RemoveRange(await db.CompanyEntities.Where(c => companyIds.Contains(c.Id)).ToListAsync());
            await db.SaveChangesAsync();
        }
        await base.CleanServer();
    }

    [TestMethod]
    public async Task EmployeeCanGeneratePreviewDownloadAndAdminCanTraceWithoutExposingOriginal()
    {
        await LoginAsAdmin();
        var qualification = await UploadQualification();
        await Http.GetAsync("/Culture/Set?culture=zh-CN&returnUrl=/Qualifications");
        var chinesePage = await Http.GetAsync("/Qualifications");
        var chineseHtml = await chinesePage.Content.ReadAsStringAsync();
        Assert.Contains(WebUtility.HtmlEncode("业务资质"), chineseHtml);
        await Http.GetAsync("/Culture/Set?culture=en-US&returnUrl=/Qualifications");
        await Http.GetAsync("/Account/LogOff");
        var (email, _) = await RegisterAndLoginAsync();
        using (var scope = Server!.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var applicant = (await manager.FindByEmailAsync(email))!;
            applicant.LegalName = "张三";
            await manager.UpdateAsync(applicant);
        }
        var generate = await PostForm("/Qualifications/Apply", new()
        {
            ["Id"] = qualification.Id.ToString(),
            ["Purpose"] = "供应商入驻登记",
            ["Recipient"] = "测试接收公司",
            ["ApplicantName"] = "伪造姓名"
        }, tokenUrl: $"/Qualifications/Apply/{qualification.Id}");
        Assert.AreEqual(HttpStatusCode.Found, generate.StatusCode);
        var record = await SingleRecord();
        Assert.AreEqual("张三", record.ApplicantName);
        Assert.AreEqual("供应商入驻登记", record.Purpose);
        Assert.AreEqual(qualification.OriginalSha256, record.OriginalSha256);
        Assert.AreEqual(0, record.LinkIssuedCount);
        var preview = await Http.GetAsync(generate.Headers.Location);
        preview.EnsureSuccessStatusCode();
        Assert.IsTrue(preview.Headers.CacheControl?.NoStore);
        var previewHtml = await preview.Content.ReadAsStringAsync();
        Assert.Contains("data-utc-time=", previewHtml);
        Assert.DoesNotContain(qualification.OriginalPath, previewHtml);
        Assert.Contains(record.GeneratedPath, previewHtml);
        var image = await PreviewImage(record.Id);
        image.EnsureSuccessStatusCode();
        var imageBytes = await image.Content.ReadAsByteArrayAsync();
        using var imageStream = new MemoryStream(imageBytes);
        Assert.AreEqual(record.Id, GetService<QualificationWatermarkService>().Detect(imageStream));
        var download = await PostForm($"/Qualifications/Download/{record.Id}", new(),
            tokenUrl: $"/Qualifications/Preview/{record.Id}");
        Assert.AreEqual(HttpStatusCode.Found, download.StatusCode);
        Assert.Contains(".zip?token=", download.Headers.Location!.OriginalString);
        using var anonymous = GetService<IHttpClientFactory>().CreateClient();
        var archiveResponse = await anonymous.GetAsync(download.Headers.Location);
        archiveResponse.EnsureSuccessStatusCode();
        Assert.AreEqual("attachment", archiveResponse.Content.Headers.ContentDisposition?.DispositionType);
        Assert.AreEqual("application/octet-stream", archiveResponse.Content.Headers.ContentType?.MediaType);
        var zipBytes = await archiveResponse.Content.ReadAsByteArrayAsync();
        record = await SingleRecord();
        Assert.AreEqual(record.ArchiveSha256, Convert.ToHexString(SHA256.HashData(zipBytes)));
        var archivedPng = ExtractPng(zipBytes);
        Assert.AreEqual(record.GeneratedSha256, Convert.ToHexString(SHA256.HashData(archivedPng)));
        using var pngStream = new MemoryStream(archivedPng);
        Assert.AreEqual(record.Id, GetService<QualificationWatermarkService>().Detect(pngStream));
        var generatedPng = await File.ReadAllBytesAsync(GetService<StorageService>().GetFilePhysicalPath(record.GeneratedPath, isVault: true));
        CollectionAssert.AreEqual(generatedPng, archivedPng);
        var repeated = await anonymous.GetByteArrayAsync(download.Headers.Location + "&w=200&square=true");
        CollectionAssert.AreEqual(zipBytes, repeated);
        Assert.AreEqual(1, (await SingleRecord()).LinkIssuedCount);
        using (var scope = Server!.Services.CreateScope())
        {
            var issue = await scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>()
                .QualificationLinkIssues.SingleAsync(i => i.QualificationDownloadId == record.Id);
            Assert.AreEqual(record.ApplicantId, issue.IssuedToId);
            Assert.AreEqual("张三", issue.IssuedToName);
        }
        var downloadWithoutToken = await PostForm($"/Qualifications/Download/{record.Id}", new(), includeToken: false);
        Assert.AreEqual(HttpStatusCode.BadRequest, downloadWithoutToken.StatusCode);
        using (var scope = Server!.Services.CreateScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<StorageService>();
            var copyToken = storage.GetToken(record.ArchivePath!, FilePermission.Download, isVault: true);
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await Http.GetAsync($"/download-private/{qualification.OriginalPath}?token={Uri.EscapeDataString(copyToken)}")).StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await Http.GetAsync($"/download-private/{qualification.OriginalPath}")).StatusCode);
            Assert.AreEqual(HttpStatusCode.NotFound, (await Http.GetAsync($"/download/{qualification.OriginalPath}")).StatusCode);
        }
        await Http.GetAsync("/Account/LogOff");
        await RegisterAndLoginAsync();
        Assert.AreEqual(HttpStatusCode.NotFound, (await Http.GetAsync($"/Qualifications/Preview/{record.Id}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await Http.GetAsync($"/Qualifications/Image/{record.Id}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await PostForm($"/Qualifications/Download/{record.Id}", new(), tokenUrl: "/")).StatusCode);
        var ownRecords = await Http.GetAsync("/Qualifications/Records");
        Assert.DoesNotContain(record.Id.ToString(), await ownRecords.Content.ReadAsStringAsync());
        AssertForbidden(await Http.GetAsync("/ManageQualifications"));
        AssertForbidden(await Http.GetAsync("/ManageQualifications/Create"));
        AssertForbidden(await Http.GetAsync("/ManageQualifications/Detect"));
        AssertForbidden(await Http.GetAsync("/ManageQualifications/Records"));
        AssertForbidden(await Http.GetAsync($"/ManageQualifications/Record/{record.Id}"));
        await Http.GetAsync("/Account/LogOff");
        await LoginAsAdmin();
        var adminEmployeeRecords = await Http.GetAsync("/Qualifications/Records");
        Assert.DoesNotContain(record.Id.ToString(), await adminEmployeeRecords.Content.ReadAsStringAsync());
        var allRecords = await Http.GetAsync("/ManageQualifications/Records");
        allRecords.EnsureSuccessStatusCode();
        Assert.Contains(record.Id.ToString(), await allRecords.Content.ReadAsStringAsync());
        var traceDetail = await Http.GetAsync($"/ManageQualifications/Record/{record.Id}");
        traceDetail.EnsureSuccessStatusCode();
        var detailBody = PageBody(await traceDetail.Content.ReadAsStringAsync());
        Assert.Contains(record.Id.ToString(), detailBody);
        Assert.DoesNotContain("/Qualifications", detailBody);
        await Http.GetAsync("/Culture/Set?culture=zh-CN&returnUrl=/ManageQualifications");
        var chineseDetail = await Http.GetAsync($"/ManageQualifications/Record/{record.Id}");
        Assert.Contains("资质生成记录详情", PageBody(WebUtility.HtmlDecode(await chineseDetail.Content.ReadAsStringAsync())));
        var detected = await Upload("/ManageQualifications/Detect", "ImagePath", "copy.png", imageBytes, new());
        detected.EnsureSuccessStatusCode();
        Assert.Contains(record.Id.ToString(), await detected.Content.ReadAsStringAsync());
        Assert.AreEqual(1, (await SingleRecord()).LinkIssuedCount);
    }

    [TestMethod]
    public async Task LegacyPngGetsZipWithoutRegeneratingWatermarkAndIssuanceTracksActualRequester()
    {
        await LoginAsAdmin();
        var qualification = await UploadQualification();
        await Http.GetAsync("/Account/LogOff");
        await RegisterAndLoginAsync();
        await PostForm("/Qualifications/Apply", new()
        {
            ["Id"] = qualification.Id.ToString(), ["Purpose"] = "Supplier registration",
            ["Recipient"] = "Test company"
        }, tokenUrl: $"/Qualifications/Apply/{qualification.Id}");
        var record = await SingleRecord();
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var tracked = await db.QualificationDownloads.SingleAsync(d => d.Id == record.Id);
            File.Delete(GetService<StorageService>().GetFilePhysicalPath(tracked.ArchivePath!, isVault: true));
            tracked.ArchivePath = null;
            tracked.ArchiveSha256 = null;
            tracked.DownloadCount = 7; // Old download statistics must not become link issuances.
            await db.SaveChangesAsync();
        }
        var first = await PostForm($"/Qualifications/Download/{record.Id}", new(), tokenUrl: $"/Qualifications/Preview/{record.Id}");
        Assert.AreEqual(HttpStatusCode.Found, first.StatusCode);
        var image = ExtractPng(await Http.GetByteArrayAsync(first.Headers.Location));
        Assert.AreEqual(record.GeneratedSha256, Convert.ToHexString(SHA256.HashData(image)));
        await Http.GetAsync("/Account/LogOff");
        await LoginAsAdmin();
        var second = await PostForm($"/Qualifications/Download/{record.Id}", new(), tokenUrl: $"/Qualifications/Preview/{record.Id}");
        Assert.AreEqual(HttpStatusCode.Found, second.StatusCode);
        Assert.AreEqual(2, (await SingleRecord()).LinkIssuedCount);
        Assert.AreEqual(7, (await SingleRecord()).DownloadCount);
        using var finalScope = Server!.Services.CreateScope();
        var context = finalScope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var issuances = await context.QualificationLinkIssues.Where(i => i.QualificationDownloadId == record.Id).ToListAsync();
        Assert.AreEqual(2, issuances.Select(i => i.IssuedToId).Distinct().Count());
        Assert.IsTrue(issuances.Any(i => i.IssuedToId == record.ApplicantId));
    }

    [TestMethod]
    public async Task InvalidUploadsAndBlankPurposeDoNotCreateRecords()
    {
        await LoginAsAdmin();
        var company = await CreateCompany();
        foreach (var (name, bytes) in new[]
        {
            ("fake.png", "not an image"u8.ToArray()), ("original.pdf", "%PDF-1.0"u8.ToArray()),
            ("small.png", ImageBytes(64, 64))
        })
        {
            var response = await Upload("/ManageQualifications/Create", "OriginalPath", name, bytes,
                new() { ["CompanyEntityId"] = company.Id.ToString(), ["Name"] = "执照", ["Version"] = "v1" });
            Assert.IsTrue(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.OK);
        }
        using (var scope = Server!.Services.CreateScope())
            Assert.AreEqual(0, await scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>().BusinessQualifications.CountAsync(q => q.CompanyEntityId == company.Id));
        var qualification = await UploadQualification();
        var rejected = await PostForm("/Qualifications/Apply", new()
        {
            ["Id"] = qualification.Id.ToString(),
            ["Purpose"] = "      ",
            ["Recipient"] = "   "
        }, tokenUrl: $"/Qualifications/Apply/{qualification.Id}");
        rejected.EnsureSuccessStatusCode();
        var missingFields = await PostForm("/Qualifications/Apply", new()
        {
            ["Id"] = qualification.Id.ToString()
        }, tokenUrl: $"/Qualifications/Apply/{qualification.Id}");
        missingFields.EnsureSuccessStatusCode();
        using var otherScope = Server!.Services.CreateScope();
        Assert.AreEqual(0, await otherScope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>().QualificationDownloads.CountAsync(d => d.BusinessQualificationId == qualification.Id));
    }

    [TestMethod]
    public async Task DisablingBlocksCopiesAndReEnablingRestoresUseWithoutLosingHistory()
    {
        await LoginAsAdmin();
        var qualification = await UploadQualification();
        await PostForm("/Qualifications/Apply", new()
        {
            ["Id"] = qualification.Id.ToString(),
            ["Purpose"] = "项目投标申请",
            ["Recipient"] = "采购公司"
        }, tokenUrl: $"/Qualifications/Apply/{qualification.Id}");
        var record = await SingleRecord();
        AssertRedirect(await PostForm($"/ManageQualifications/Retire/{qualification.Id}", new(), tokenUrl: "/ManageQualifications"), "/ManageQualifications");
        Assert.AreEqual(HttpStatusCode.NotFound, (await Http.GetAsync($"/Qualifications/Apply/{qualification.Id}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await Http.GetAsync($"/Qualifications/Image/{record.Id}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await PostForm($"/Qualifications/Download/{record.Id}", new(), tokenUrl: "/ManageQualifications/Create")).StatusCode);
        var records = await Http.GetAsync("/Qualifications/Records");
        Assert.Contains(record.Id.ToString(), await records.Content.ReadAsStringAsync());
        var deleteCompany = await PostForm($"/CompanyEntity/Delete/{qualification.CompanyEntityId}", new(), tokenUrl: "/CompanyEntity/Manage");
        deleteCompany.EnsureSuccessStatusCode();
        using var scope = Server!.Services.CreateScope();
        Assert.IsTrue(await scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>().CompanyEntities.AnyAsync(c => c.Id == qualification.CompanyEntityId));

        var management = await Http.GetAsync("/ManageQualifications");
        var managementHtml = await management.Content.ReadAsStringAsync();
        Assert.Contains($"/ManageQualifications/Enable/{qualification.Id}", managementHtml);
        Assert.DoesNotContain($"/Qualifications/Apply/{qualification.Id}", managementHtml);
        var employeeList = await Http.GetAsync("/Qualifications");
        Assert.DoesNotContain($"/Qualifications/Apply/{qualification.Id}", await employeeList.Content.ReadAsStringAsync());
        var rejectedGeneration = await PostForm("/Qualifications/Apply", new()
        {
            ["Id"] = qualification.Id.ToString(),
            ["Purpose"] = "停用证照使用测试",
            ["Recipient"] = "采购公司"
        }, tokenUrl: "/ManageQualifications/Create");
        Assert.AreEqual(HttpStatusCode.NotFound, rejectedGeneration.StatusCode);
        var disabledPreview = await Http.GetAsync($"/Qualifications/Preview/{record.Id}");
        disabledPreview.EnsureSuccessStatusCode();
        var disabledHtml = await disabledPreview.Content.ReadAsStringAsync();
        Assert.DoesNotContain($"/Qualifications/Image/{record.Id}", disabledHtml);
        Assert.DoesNotContain($"/Qualifications/Download/{record.Id}", disabledHtml);

        Assert.AreEqual(HttpStatusCode.BadRequest, (await PostForm($"/ManageQualifications/Enable/{qualification.Id}", new(), includeToken: false)).StatusCode);
        AssertRedirect(await PostForm($"/ManageQualifications/Enable/{qualification.Id}", new(), tokenUrl: "/ManageQualifications"), "/ManageQualifications");
        (await Http.GetAsync($"/Qualifications/Apply/{qualification.Id}")).EnsureSuccessStatusCode();
        (await PreviewImage(record.Id)).EnsureSuccessStatusCode();
        Assert.AreEqual(HttpStatusCode.Found, (await PostForm($"/Qualifications/Download/{record.Id}", new(), tokenUrl: $"/Qualifications/Preview/{record.Id}")).StatusCode);
        Assert.AreEqual(1, (await SingleRecord()).LinkIssuedCount);
        Assert.AreEqual(record.Id, (await SingleRecord()).Id);
        var reEnabledManage = await Http.GetAsync("/ManageQualifications");
        var reEnabledHtml = await reEnabledManage.Content.ReadAsStringAsync();
        Assert.Contains($"/ManageQualifications/Retire/{qualification.Id}", reEnabledHtml);
        Assert.DoesNotContain($"/ManageQualifications/Enable/{qualification.Id}", reEnabledHtml);
        Assert.DoesNotContain($"/Qualifications/Apply/{qualification.Id}", reEnabledHtml);
        var reEnabledEmployee = await Http.GetAsync("/Qualifications");
        var employeeHtml = await reEnabledEmployee.Content.ReadAsStringAsync();
        Assert.Contains($"/Qualifications/Apply/{qualification.Id}", employeeHtml);
        Assert.DoesNotContain($"/ManageQualifications/Retire/{qualification.Id}", employeeHtml);
        Assert.DoesNotContain($"/ManageQualifications/Enable/{qualification.Id}", employeeHtml);
        Assert.Contains("Generate Watermark and Download", employeeHtml);
        await Http.GetAsync("/Culture/Set?culture=zh-CN&returnUrl=/ManageQualifications");
        var chineseManage = await Http.GetAsync("/ManageQualifications");
        Assert.Contains("停止使用", WebUtility.HtmlDecode(await chineseManage.Content.ReadAsStringAsync()));
        var chineseEmployee = await Http.GetAsync("/Qualifications");
        Assert.Contains("生成水印并下载", WebUtility.HtmlDecode(await chineseEmployee.Content.ReadAsStringAsync()));
    }

    [TestMethod]
    public async Task AnonymousAndCrossSiteRequestsCannotGenerateOrDownload()
    {
        Assert.AreEqual(HttpStatusCode.Found, (await Http.GetAsync("/Qualifications")).StatusCode);
        await LoginAsAdmin();
        var qualification = await UploadQualification();
        var withoutToken = await PostForm("/Qualifications/Apply", new()
        {
            ["Id"] = qualification.Id.ToString(),
            ["Purpose"] = "业务资质测试",
            ["Recipient"] = "测试公司"
        }, includeToken: false);
        Assert.AreEqual(HttpStatusCode.BadRequest, withoutToken.StatusCode);
        await Http.GetAsync("/Account/LogOff");
        await RegisterAndLoginAsync();
        AssertForbidden(await PostForm($"/ManageQualifications/Retire/{qualification.Id}", new(), tokenUrl: "/"));
        AssertForbidden(await PostForm($"/ManageQualifications/Enable/{qualification.Id}", new(), tokenUrl: "/"));
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var stored = await db.BusinessQualifications.SingleAsync(q => q.Id == qualification.Id);
            stored.IsActive = false;
            await db.SaveChangesAsync();
        }
        AssertForbidden(await PostForm($"/ManageQualifications/Enable/{qualification.Id}", new(), tokenUrl: "/"));
        using (var scope = Server!.Services.CreateScope())
        {
            Assert.IsFalse((await scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>()
                .BusinessQualifications.SingleAsync(q => q.Id == qualification.Id)).IsActive);
        }
        AssertForbidden(await PostForm("/ManageQualifications/Create", new(), tokenUrl: "/"));
        AssertForbidden(await PostForm("/ManageQualifications/Detect", new(), tokenUrl: "/"));
    }

    [TestMethod]
    public async Task TamperedOriginalFailsClosedWithoutIssuingCopy()
    {
        await LoginAsAdmin();
        var qualification = await UploadQualification();
        await File.WriteAllBytesAsync(savedFiles.Last(), ImageBytes(800, 800));
        var rejected = await PostForm("/Qualifications/Apply", new()
        {
            ["Id"] = qualification.Id.ToString(),
            ["Purpose"] = "供应商入驻登记",
            ["Recipient"] = "测试公司"
        }, tokenUrl: $"/Qualifications/Apply/{qualification.Id}");
        rejected.EnsureSuccessStatusCode();
        using var scope = Server!.Services.CreateScope();
        Assert.AreEqual(0, await scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>().QualificationDownloads
            .CountAsync(d => d.BusinessQualificationId == qualification.Id));
    }

    [TestMethod]
    public async Task EmployeeAndManagementPagesRemainSeparateForAdministrators()
    {
        await LoginAsAdmin();
        var qualification = await UploadQualification();
        var employee = await Http.GetAsync("/Qualifications");
        var employeeBody = PageBody(await employee.Content.ReadAsStringAsync());
        Assert.Contains($"/Qualifications/Apply/{qualification.Id}", employeeBody);
        Assert.Contains("My Download Records", employeeBody);
        Assert.DoesNotContain("/ManageQualifications", employeeBody);
        Assert.DoesNotContain("Employee View", employeeBody);
        Assert.DoesNotContain("Add Qualification", employeeBody);
        Assert.DoesNotContain("Detect Watermark", employeeBody);
        Assert.DoesNotContain("Stop Use", employeeBody);

        var management = await Http.GetAsync("/ManageQualifications");
        var managementBody = PageBody(await management.Content.ReadAsStringAsync());
        Assert.Contains("Manage Business Qualifications", managementBody);
        Assert.Contains("/ManageQualifications/Create", managementBody);
        Assert.Contains("Add Qualification", managementBody);
        Assert.Contains("/ManageQualifications/Detect", managementBody);
        Assert.Contains("/ManageQualifications/Records", managementBody);
        Assert.Contains($"/ManageQualifications/Retire/{qualification.Id}", managementBody);
        Assert.DoesNotContain("/Qualifications", managementBody);
        Assert.DoesNotContain("Employee View", managementBody);
        Assert.DoesNotContain("Generate Watermark and Download", managementBody);

        var records = await Http.GetAsync("/ManageQualifications/Records");
        Assert.Contains("All Download Records", PageBody(await records.Content.ReadAsStringAsync()));
        foreach (var oldUrl in new[] { "/Qualifications/Manage", "/Qualifications/Create", "/Qualifications/Detect" })
            Assert.AreEqual(HttpStatusCode.NotFound, (await Http.GetAsync(oldUrl)).StatusCode);
        await Http.GetAsync("/Culture/Set?culture=zh-CN&returnUrl=/ManageQualifications");
        foreach (var (url, title) in new[]
        {
            ("/Qualifications", "业务资质"), ("/ManageQualifications", "管理业务资质"),
            ("/ManageQualifications/Create", "添加资质"), ("/ManageQualifications/Detect", "检测隐形水印"),
            ("/ManageQualifications/Records", "全部下载记录"), ("/Qualifications/Records", "我的下载记录")
        })
        {
            var response = await Http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            Assert.Contains(title, PageBody(WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync())), url);
        }
    }

    [TestMethod]
    public async Task UploadAndDetectFormsUseFrameworkUploadAndModelValidation()
    {
        await LoginAsAdmin();
        foreach (var (url, field) in new[]
        {
            ("/ManageQualifications/Create", "OriginalPath"), ("/ManageQualifications/Detect", "ImagePath")
        })
        {
            var response = await Http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains($"name=\"{field}\"", html);
            Assert.Contains("data-allowed-file-extensions=\"png jpg jpeg\"", html);
            Assert.Contains("data-max-file-size=\"20M\"", html);
            Assert.Contains("/upload-private/qualifications/", html);
            Assert.Contains("data-val-required=", html);
            Assert.Contains($"data-valmsg-for=\"{field}\"", html);
            Assert.DoesNotContain("enctype=\"multipart/form-data\"", html);
            Assert.IsFalse(Regex.IsMatch(html, $"<input[^>]*type=\"file\"[^>]*name=\"{field}\""));
        }
    }

    [TestMethod]
    [DataRow("zh-CN", "en-US")]
    [DataRow("en-US", "zh-HK")]
    [DataRow("zh-HK", "zh-CN")]
    [DataRow("zh-CN", "auto")]
    public async Task EmployeeCanSelectWatermarkLanguageIndependentlyOfInterface(string uiCulture, string language)
    {
        await LoginAsAdmin();
        var qualification = await UploadQualification();
        await Http.GetAsync($"/Culture/Set?culture={uiCulture}&returnUrl=/Qualifications");
        var applyUrl = $"/Qualifications/Apply/{qualification.Id}";
        var page = await Http.GetAsync(applyUrl);
        var pageHtml = await page.Content.ReadAsStringAsync();
        Assert.Contains("name=\"WatermarkLanguage\"", pageHtml);
        Assert.Contains("data-valmsg-for=\"WatermarkLanguage\"", pageHtml);
        Assert.IsTrue(Regex.IsMatch(pageHtml, "<option[^>]*selected=\"selected\"[^>]*value=\"auto\"|<option[^>]*value=\"auto\"[^>]*selected=\"selected\""));
        foreach (var choice in new[] { "auto", "zh-CN", "zh-HK", "en-US" })
            Assert.Contains($"value=\"{choice}\"", pageHtml);
        var response = await PostForm("/Qualifications/Apply", new()
        {
            ["Id"] = qualification.Id.ToString(), ["Purpose"] = "Supplier onboarding",
            ["Recipient"] = "Test recipient", ["WatermarkLanguage"] = language
        }, tokenUrl: applyUrl);
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        var record = await SingleRecord();
        var download = await PostForm($"/Qualifications/Download/{record.Id}", new(), tokenUrl: $"/Qualifications/Preview/{record.Id}");
        Assert.AreEqual(HttpStatusCode.Found, download.StatusCode);
        var actual = ExtractPng(await Http.GetByteArrayAsync(download.Headers.Location));
        var originalCulture = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(uiCulture);
            using var source = File.OpenRead(savedFiles.Last());
            var expected = GetService<QualificationWatermarkService>().Generate(source, new QualificationWatermarkRequest(
                record.Purpose, record.Recipient, record.ApplicantName, record.CreatedAt, record.Id, language));
            CollectionAssert.AreEqual(expected, actual);
            Assert.AreEqual(uiCulture, System.Globalization.CultureInfo.CurrentUICulture.Name);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = originalCulture;
        }
        var unchangedPage = await Http.GetAsync(applyUrl);
        Assert.Contains($"lang=\"{uiCulture}\"", await unchangedPage.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task InvalidLanguageDoesNotIssueCopyAndValidationPreservesChosenLanguage()
    {
        await LoginAsAdmin();
        var qualification = await UploadQualification();
        var applyUrl = $"/Qualifications/Apply/{qualification.Id}";
        foreach (var language in new[] { "fr-FR", "en", "", "EN-US" })
        {
            var response = await PostForm("/Qualifications/Apply", new()
            {
                ["Id"] = qualification.Id.ToString(), ["Purpose"] = "Supplier onboarding",
                ["Recipient"] = "Test recipient", ["WatermarkLanguage"] = language
            }, tokenUrl: applyUrl);
            response.EnsureSuccessStatusCode();
            Assert.Contains("Select a supported watermark language.", await response.Content.ReadAsStringAsync());
        }
        var invalidPurpose = await PostForm("/Qualifications/Apply", new()
        {
            ["Id"] = qualification.Id.ToString(), ["Purpose"] = " ",
            ["Recipient"] = "Test recipient", ["WatermarkLanguage"] = "en-US"
        }, tokenUrl: applyUrl);
        var html = await invalidPurpose.Content.ReadAsStringAsync();
        Assert.IsTrue(Regex.IsMatch(html, "<option[^>]*selected=\"selected\"[^>]*value=\"en-US\"|<option[^>]*value=\"en-US\"[^>]*selected=\"selected\""));
        using var scope = Server!.Services.CreateScope();
        Assert.AreEqual(0, await scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>()
            .QualificationDownloads.CountAsync(d => d.BusinessQualificationId == qualification.Id));
    }

    [TestMethod]
    public async Task UnmarkedImageHasNoMatchAndDetectionDoesNotPersistUpload()
    {
        await LoginAsAdmin();
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var before = await db.QualificationDownloads.CountAsync();
        var detected = await Upload("/ManageQualifications/Detect", "ImagePath", "unmarked.png", ImageBytes(), new());
        detected.EnsureSuccessStatusCode();
        Assert.AreEqual(before, await db.QualificationDownloads.CountAsync());
        var html = WebUtility.HtmlDecode(await detected.Content.ReadAsStringAsync());
        Assert.Contains("No verifiable issuance record was found", html);
        Assert.IsFalse(File.Exists(savedFiles.Last()));
    }

    [TestMethod]
    public async Task BusinessIssuesUploadGrantsOnlyToManagersAndScopesSubmittedPaths()
    {
        await LoginAsAdmin();
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var adminId = (await db.Users.SingleAsync(u => u.Email == "admin@default.com")).Id;
        var storage = scope.ServiceProvider.GetRequiredService<StorageService>();
        var ownerFolder = QualificationFileService.UploadSubfolder(adminId);
        var url = storage.GetUploadUrl(ownerFolder, isVault: true, maxSizeInMb: 20, allowedExtensions: "png jpg jpeg");
        Assert.DoesNotContain("qualifications/originals", await (await Http.GetAsync("/ManageQualifications/Create")).Content.ReadAsStringAsync());
        await Http.GetAsync("/Account/LogOff");
        using (var content = ImageContent())
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await Http.PostAsync(url.Replace("token=", "token=invalid"), content)).StatusCode);
        await RegisterAndLoginAsync();
        AssertForbidden(await Http.GetAsync("/ManageQualifications/Create"));
        AssertForbidden(await Http.GetAsync("/ManageQualifications/Detect"));
    }

    [TestMethod]
    public async Task ForgedLogicalPathsCannotReuseOriginalsCopiesOrOtherUsersUploads()
    {
        await LoginAsAdmin();
        var company = await CreateCompany();
        var uploaded = await UploadFile("/ManageQualifications/Create", "image.png", ImageBytes());
        uploaded.EnsureSuccessStatusCode();
        var path = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
            await uploaded.Content.ReadAsStringAsync())!["Path"];
        foreach (var forged in new[]
        {
            "../outside.png", "company-certs/image.png", "qualifications/originals/image.png",
            "qualifications/copies/image.png", path.Replace(path.Split('/')[2], new string('f', 64))
        })
        {
            var response = await PostForm("/ManageQualifications/Create", new()
            {
                ["CompanyEntityId"] = company.Id.ToString(), ["Name"] = "License", ["Version"] = "v1", ["OriginalPath"] = forged
            });
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("data-default-file=", PageBody(html));
        }
        using var scope = Server!.Services.CreateScope();
        Assert.AreEqual(0, await scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>()
            .BusinessQualifications.CountAsync(q => q.CompanyEntityId == company.Id));
    }

    [TestMethod]
    public async Task AbandonedUploadsAreCleanedWithoutDeletingOriginals()
    {
        await LoginAsAdmin();
        var qualification = await UploadQualification();
        var uploaded = await UploadFile("/ManageQualifications/Detect", "abandoned.png", ImageBytes());
        uploaded.EnsureSuccessStatusCode();
        var abandoned = savedFiles.Last();
        File.SetLastWriteTimeUtc(abandoned, DateTime.UtcNow.AddHours(-2));
        var files = GetService<QualificationFileService>();
        files.DeleteExpiredUploads(DateTime.UtcNow.AddHours(-1));
        Assert.IsFalse(File.Exists(abandoned));
        Assert.IsTrue(File.Exists(GetService<StorageService>().GetFilePhysicalPath(qualification.OriginalPath, isVault: true)));
    }

    private static MultipartFormDataContent ImageContent()
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(ImageBytes()), "file", "image.png");
        return content;
    }

    private async Task<BusinessQualification> UploadQualification()
    {
        var company = await CreateCompany();
        var response = await Upload("/ManageQualifications/Create", "OriginalPath", "license.png", ImageBytes(),
            new() { ["CompanyEntityId"] = company.Id.ToString(), ["Name"] = "营业执照", ["Version"] = "2026-v1" });
        AssertRedirect(response, "/ManageQualifications");
        using var scope = Server!.Services.CreateScope();
        var qualification = await scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>().BusinessQualifications
            .SingleAsync(q => q.CompanyEntityId == company.Id);
        qualificationId = qualification.Id;
        savedFiles.Add(scope.ServiceProvider.GetRequiredService<StorageService>().GetFilePhysicalPath(qualification.OriginalPath, isVault: true));
        return qualification;
    }

    private async Task<CompanyEntity> CreateCompany()
    {
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var company = new CompanyEntity { CompanyName = "测试主体", EntityCode = Guid.NewGuid().ToString("N") };
        db.CompanyEntities.Add(company);
        await db.SaveChangesAsync();
        companyIds.Add(company.Id);
        return company;
    }

    private async Task<QualificationDownload> SingleRecord()
    {
        using var scope = Server!.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>().QualificationDownloads.SingleAsync(d => d.BusinessQualificationId == qualificationId);
    }

    private async Task<HttpResponseMessage> PreviewImage(Guid id)
    {
        var html = await (await Http.GetAsync($"/Qualifications/Preview/{id}")).Content.ReadAsStringAsync();
        var match = Regex.Match(html, "<img src=\"([^\"]+)\" class=\"img-fluid mb-3\"");
        Assert.IsTrue(match.Success);
        return await Http.GetAsync(WebUtility.HtmlDecode(match.Groups[1].Value));
    }

    private static byte[] ExtractPng(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.AreEqual(1, zip.Entries.Count);
        Assert.EndsWith(".png", zip.Entries[0].Name);
        using var input = zip.Entries[0].Open();
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    private async Task<HttpResponseMessage> Upload(string url, string field, string fileName, byte[] image, Dictionary<string, string> fields)
    {
        var uploaded = await UploadFile(url, fileName, image);
        if (!uploaded.IsSuccessStatusCode) return uploaded;
        var path = (await uploaded.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["Path"];
        fields[field] = path;
        return await PostForm(url, fields);
    }

    private async Task<HttpResponseMessage> UploadFile(string pageUrl, string fileName, byte[] image)
    {
        var page = await Http.GetAsync(pageUrl);
        page.EnsureSuccessStatusCode();
        var pageHtml = await page.Content.ReadAsStringAsync();
        var endpoint = Regex.Match(pageHtml, @"uploadUrl: '([^']+)'").Groups[1].Value;
        Assert.IsFalse(string.IsNullOrWhiteSpace(endpoint), "Missing framework upload endpoint.");
        endpoint = WebUtility.HtmlDecode(endpoint);
        using var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(image);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(bytes, "file", fileName);
        var response = await Http.PostAsync(endpoint, content);
        if (response.IsSuccessStatusCode)
        {
            var result = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                await response.Content.ReadAsStringAsync())!;
            Assert.Contains("/download-private/qualifications/", result["InternetPath"]);
            Assert.DoesNotContain("qualifications/originals", result["InternetPath"]);
            savedFiles.Add(GetService<StorageService>().GetFilePhysicalPath(result["Path"], isVault: true));
        }
        return response;
    }

    private static byte[] ImageBytes(int width = 768, int height = 768)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint();
        paint.Color = SKColors.DarkSlateGray;
        paint.StrokeWidth = 3;
        for (var y = 40; y < height - 40; y += 30) canvas.DrawLine(40, y, width - 40, y, paint);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string PageBody(string html) => Regex.Match(html, @"<main\b[^>]*>([\s\S]*?)</main>").Groups[1].Value;

    private static void AssertForbidden(HttpResponseMessage response)
    {
        Assert.IsTrue(response.StatusCode == HttpStatusCode.Forbidden ||
            response.StatusCode == HttpStatusCode.Found && response.Headers.Location!.OriginalString.Contains("Code403"));
    }
}
