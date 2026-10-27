using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SafeLearnKids.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class QuestionsController : Controller
{
    [HttpGet("admin-questions")]
    public IActionResult Index() => View();

}
