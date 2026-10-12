using Aiursoft.EmployeeCenter.Models.HomeViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace Aiursoft.EmployeeCenter.Controllers;

[LimitPerMin]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return this.SimpleView(new IndexViewModel());
    }

    public IActionResult SelfHost()
    {
        return this.SimpleView(new IndexViewModel());
    }
}
