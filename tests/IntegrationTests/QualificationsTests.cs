using System.Net.Http.Headers;
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
            var storage = scope.ServiceProvider.GetRequiredService<StorageService>();
            var records = await db.QualificationDownloads.Where(d => d.BusinessQualificationId == qualificationId).ToListAsync();
            foreach (var record in records)
                File.Delete(storage.GetFilePhysicalPath(record.GeneratedPath, isVault: true));
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
        Assert.Contains(System.Net.WebUtility.HtmlEncode("业务资质"), chineseHtml);
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
        Assert.AreEqual(0, record.DownloadCount);
        var preview = await Http.GetAsync(generate.Headers.Location);
        preview.EnsureSuccessStatusCode();
        Assert.IsTrue(preview.Headers.CacheControl?.NoStore);
        var previewHtml = await preview.Content.ReadAsStringAsync();
        Assert.DoesNotContain(qualification.OriginalPath, previewHtml);
        Assert.DoesNotContain(record.GeneratedPath, previewHtml);
        var image = await Http.GetAsync($"/Qualifications/Image/{record.Id}");
        image.EnsureSuccessStatusCode();
        var imageBytes = await image.Content.ReadAsByteArrayAsync();
        using var imageStream = new MemoryStream(imageBytes);
        Assert.AreEqual(record.Id, GetService<QualificationWatermarkService>().Detect(imageStream));
        var download = await PostForm($"/Qualifications/Download/{record.Id}", new(),
            tokenUrl: $"/Qualifications/Preview/{record.Id}");
        download.EnsureSuccessStatusCode();
        Assert.AreEqual("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        CollectionAssert.AreEqual(imageBytes, await download.Content.ReadAsByteArrayAsync());
        Assert.AreEqual(1, (await SingleRecord()).DownloadCount);
        var downloadWithoutToken = await PostForm($"/Qualifications/Download/{record.Id}", new(), includeToken: false);
        Assert.AreEqual(HttpStatusCode.BadRequest, downloadWithoutToken.StatusCode);
        using (var scope = Server!.Services.CreateScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<StorageService>();
            var sourceToken = storage.GetToken(qualification.OriginalPath, FilePermission.Download, isVault: true);
            var copyToken = storage.GetToken(record.GeneratedPath, FilePermission.Download, isVault: true);
            Assert.AreEqual(HttpStatusCode.NotFound, (await Http.GetAsync($"/download-private/{qualification.OriginalPath}?token={Uri.EscapeDataString(sourceToken)}")).StatusCode);
            Assert.AreEqual(HttpStatusCode.NotFound, (await Http.GetAsync($"/download-private/{record.GeneratedPath}?token={Uri.EscapeDataString(copyToken)}")).StatusCode);
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
        Assert.Contains("资质生成记录详情", PageBody(System.Net.WebUtility.HtmlDecode(await chineseDetail.Content.ReadAsStringAsync())));
        var detected = await Upload("/ManageQualifications/Detect", "Image", "copy.png", imageBytes, new());
        detected.EnsureSuccessStatusCode();
        Assert.Contains(record.Id.ToString(), await detected.Content.ReadAsStringAsync());
        Assert.AreEqual(1, (await SingleRecord()).DownloadCount);
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
            var response = await Upload("/ManageQualifications/Create", "Original", name, bytes,
                new() { ["CompanyEntityId"] = company.Id.ToString(), ["Name"] = "执照", ["Version"] = "v1" });
            response.EnsureSuccessStatusCode();
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
        (await Http.GetAsync($"/Qualifications/Image/{record.Id}")).EnsureSuccessStatusCode();
        (await PostForm($"/Qualifications/Download/{record.Id}", new(), tokenUrl: $"/Qualifications/Preview/{record.Id}")).EnsureSuccessStatusCode();
        Assert.AreEqual(1, (await SingleRecord()).DownloadCount);
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
        Assert.Contains("停止使用", System.Net.WebUtility.HtmlDecode(await chineseManage.Content.ReadAsStringAsync()));
        var chineseEmployee = await Http.GetAsync("/Qualifications");
        Assert.Contains("生成水印并下载", System.Net.WebUtility.HtmlDecode(await chineseEmployee.Content.ReadAsStringAsync()));
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
        await File.WriteAllBytesAsync(savedFiles.Single(), ImageBytes(800, 800));
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
            Assert.Contains(title, PageBody(System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync())), url);
        }
    }

    [TestMethod]
    public async Task UploadAndDetectFormsUseMimeTypesForClientFileValidation()
    {
        await LoginAsAdmin();
        foreach (var (url, field) in new[]
        {
            ("/ManageQualifications/Create", "Original"), ("/ManageQualifications/Detect", "Image")
        })
        {
            var response = await Http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            var input = Regex.Match(html, $"<input\\b[^>]*\\bname=\"{field}\"[^>]*>").Value;
            Assert.IsFalse(string.IsNullOrEmpty(input), $"Missing file input on {url}");
            Assert.Contains("accept=\"image/png,image/jpeg\"", input,
                "UIStack validates the accept attribute against File.type, not the filename extension.");
        }
    }

    [TestMethod]
    public async Task UnmarkedImageHasNoMatchAndDetectionDoesNotPersistUpload()
    {
        await LoginAsAdmin();
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var before = await db.QualificationDownloads.CountAsync();
        var detected = await Upload("/ManageQualifications/Detect", "Image", "unmarked.png", ImageBytes(), new());
        detected.EnsureSuccessStatusCode();
        Assert.AreEqual(before, await db.QualificationDownloads.CountAsync());
        var html = System.Net.WebUtility.HtmlDecode(await detected.Content.ReadAsStringAsync());
        Assert.Contains("未找到可验证的对应记录", html);
    }

    private async Task<BusinessQualification> UploadQualification()
    {
        var company = await CreateCompany();
        var response = await Upload("/ManageQualifications/Create", "Original", "license.png", ImageBytes(),
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

    private async Task<HttpResponseMessage> Upload(string url, string field, string fileName, byte[] image, Dictionary<string, string> fields)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(await GetAntiCsrfToken(url)), "__RequestVerificationToken");
        foreach (var (key, value) in fields) content.Add(new StringContent(value), key);
        var bytes = new ByteArrayContent(image);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(bytes, field, fileName);
        return await Http.PostAsync(url, content);
    }

    private static byte[] ImageBytes(int width = 768, int height = 768)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.DarkSlateGray, StrokeWidth = 3 };
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
