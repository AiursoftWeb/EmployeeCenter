using Aiursoft.EmployeeCenter.Services.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
public class ExportController(ExportService exportService) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync()
    {
        await exportService.ExportAllForUser(User);
        return Json(new { success = true, message = "Synchronization complete!" });
    }
}
