using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.DnsAudit;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanAuditDns)]
[LimitPerMin]
public sealed class DnsAuditController(
    CloudflareDnsAuditService auditService,
    ILogger<DnsAuditController> logger) : Controller
{
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Development",
        CascadedLinksIcon = "git-branch",
        CascadedLinksOrder = 2,
        LinkText = "DNS Audit",
        LinkOrder = 3)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var isConfigured = await auditService.IsConfiguredAsync();
        if (!isConfigured)
        {
            return this.StackView(new DnsAuditIndexViewModel
            {
                IsConfigured = false
            });
        }

        try
        {
            var report = await auditService.AuditAsync(cancellationToken);
            return this.StackView(new DnsAuditIndexViewModel
            {
                IsConfigured = true,
                Report = report
            });
        }
        catch (CloudflareDnsAuditException ex)
        {
            logger.LogWarning(ex, "Cloudflare DNS audit failed");
            return this.StackView(new DnsAuditIndexViewModel
            {
                IsConfigured = true,
                ErrorMessage = ex.Message
            });
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Cloudflare DNS audit could not reach the API");
            return this.StackView(new DnsAuditIndexViewModel
            {
                IsConfigured = true,
                ErrorMessage = "Cloudflare API could not be reached. Try the audit again later."
            });
        }
    }
}
