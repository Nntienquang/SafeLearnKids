using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SafeLearnKids.Web.Areas.Parent.Controllers;

[Area("Parent")]
[Authorize(Roles = "Parent")]
public class HistoryController : Controller
{
    [HttpGet("history")]
    public IActionResult Index() => View();

}
