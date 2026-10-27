using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SafeLearnKids.Web.Areas.Parent.Controllers;

[Area("Parent")]
[Authorize(Roles = "Parent")]
public class TimeLimitsController : Controller
{
    [HttpGet("time")]
    public IActionResult Index() => View();

}
