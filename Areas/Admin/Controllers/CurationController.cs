using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SafeLearnKids.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CurationController : Controller
{
    [HttpGet("admin-curate")]
    public IActionResult Index() => View();

}
