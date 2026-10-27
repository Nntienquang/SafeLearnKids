using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SafeLearnKids.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CatalogController : Controller
{
    [HttpGet("admin-catalog")]
    public IActionResult Index() => View();

    [HttpGet("admin-lesson")]
    public IActionResult Lesson() => View();

}
