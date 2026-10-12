using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Models.CertificateViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
[LimitPerMin]
public class ManageCertificateController : Controller
{
    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanPrintCertificates)]
    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 3,
        CascadedLinksGroupName = "Legal",
        CascadedLinksIcon = "scale",
        CascadedLinksOrder = 5,
        LinkText = "Certificate Print",
        LinkOrder = 2)]
    public IActionResult Index()
    {
        return this.StackView(new IndexViewModel());
    }
}
