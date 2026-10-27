using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SafeLearnKids.Web.Areas.Parent.Controllers;

[Area("Parent")]
[Authorize(Roles = "Parent")]
public class ChildrenController : Controller
{
    [HttpGet("onboarding")]
    public IActionResult Onboarding() => View();

    [HttpGet("profiles")]
    public IActionResult Index() => View();

}
