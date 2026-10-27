using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SafeLearnKids.Web.Areas.Parent.Controllers;

[Area("Parent")]
[Authorize(Roles = "Parent")]
public class LibraryController : Controller
{
    [HttpGet("library")]
    public IActionResult Index() => View();

    [HttpGet("lesson-detail")]
    public IActionResult Details() => View();

}
