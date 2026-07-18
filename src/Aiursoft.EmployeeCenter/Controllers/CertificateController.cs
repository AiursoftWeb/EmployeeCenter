using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.CertificateViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Microsoft.Extensions.Localization;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
[LimitPerMin]
public class CertificateController(
    UserManager<User> userManager,
    IOptions<AppSettings> appSettings,
    IStringLocalizer<CertificateController> localizer) : Controller
{
    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanPrintSelfCertificates)]
    [RenderInNavBar(
        NavGroupName = "Personal",
        NavGroupOrder = 2,
        CascadedLinksGroupName = "Print Certificate",
        CascadedLinksIcon = "printer",
        CascadedLinksOrder = 2,
        LinkText = "Certificate of Employment",
        LinkOrder = 1)]
    public async Task<IActionResult> Employment()
    {
        var user = await userManager.Users
            .Include(u => u.SigningEntity)
            .FirstOrDefaultAsync(u => u.Id == userManager.GetUserId(User));
        return this.StackView(new PrintViewModel
        {
            TargetUser = user,
            Type = CertificateType.Employment,
            CompanyName = user?.SigningEntity?.CompanyName ?? appSettings.Value.CompanyName,
            CompanyNameEnglish = user?.SigningEntity?.CompanyNameEnglish,
            PageTitle = localizer["Print Certificate of Employment"]
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanPrintSelfCertificates)]
    [RenderInNavBar(
        NavGroupName = "Personal",
        NavGroupOrder = 2,
        CascadedLinksGroupName = "Print Certificate",
        CascadedLinksIcon = "printer",
        CascadedLinksOrder = 2,
        LinkText = "Certificate of Income",
        LinkOrder = 2)]
    public async Task<IActionResult> Income()
    {
        var user = await userManager.Users
            .Include(u => u.SigningEntity)
            .FirstOrDefaultAsync(u => u.Id == userManager.GetUserId(User));
        return this.StackView(new PrintViewModel
        {
            TargetUser = user,
            Type = CertificateType.Income,
            CompanyName = user?.SigningEntity?.CompanyName ?? appSettings.Value.CompanyName,
            CompanyNameEnglish = user?.SigningEntity?.CompanyNameEnglish,
            PageTitle = localizer["Print Certificate of Income"]
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Print(CertificateType type, string lang, string? userId)
    {
        User? targetUser;
        if (string.IsNullOrEmpty(userId))
        {
            if (!User.HasClaim(AppPermissions.Type, AppPermissionNames.CanPrintSelfCertificates))
            {
                return Forbid();
            }
            targetUser = await userManager.Users
                .Include(u => u.SigningEntity)
                .FirstOrDefaultAsync(u => u.Id == userManager.GetUserId(User));
        }
        else
        {
            if (!User.HasClaim(AppPermissions.Type, AppPermissionNames.CanPrintCertificates))
            {
                return Forbid();
            }
            targetUser = await userManager.Users
                .Include(u => u.SigningEntity)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        if (targetUser == null)
        {
            return NotFound();
        }

        return View(new PrintViewModel
        {
            TargetUser = targetUser,
            Type = type,
            CertificateLanguage = lang,
            CompanyName = targetUser.SigningEntity?.CompanyName ?? appSettings.Value.CompanyName,
            CompanyNameEnglish = targetUser.SigningEntity?.CompanyNameEnglish,
            PageTitle = type == CertificateType.Employment ? "Certificate of Employment" : "Certificate of Income"
        });
    }
}
