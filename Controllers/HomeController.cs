using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SafeLearnKids.Web.Controllers;

public class HomeController : Controller
{
    [AllowAnonymous]
    [HttpGet("")]
    public IActionResult Index() => View();

    [AllowAnonymous]
    [HttpGet("design-system")]
    public IActionResult DesignSystem() => View();

    [AllowAnonymous]
    [HttpGet("states")]
    public IActionResult States() => View();

    [AllowAnonymous]
    [Route("error/{code?}")]
    public IActionResult Error(int? code)
    {
        Response.StatusCode = code ?? 500;
        return View("NotFound");
    }
}
