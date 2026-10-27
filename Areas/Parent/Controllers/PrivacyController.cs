using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SafeLearnKids.Web.Areas.Parent.Controllers;

[Area("Parent")]
[Authorize(Roles = "Parent")]
public class PrivacyController : Controller
{
    [HttpGet("privacy")]
    public IActionResult Index() => View();

}
