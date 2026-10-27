using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SafeLearnKids.Web.Areas.Parent.Controllers;

[Area("Parent")]
[Authorize(Roles = "Parent")]
public class SourcesController : Controller
{
    [HttpGet("sources")]
    public IActionResult Index() => View();

}
