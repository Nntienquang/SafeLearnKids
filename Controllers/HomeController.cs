using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SafeLearnKids.Web.Controllers;

// The design-system and state gallery pages are developer tools; outside Development they answer 404.
public class HomeController(IWebHostEnvironment environment) : Controller
{
    [AllowAnonymous]
    [HttpGet("")]
    public IActionResult Index() => View();

    [AllowAnonymous]
    [HttpGet("design-system")]
    public IActionResult DesignSystem() => environment.IsDevelopment() ? View() : NotFound();

    [AllowAnonymous]
    [HttpGet("states")]
    public IActionResult States() => environment.IsDevelopment() ? View() : NotFound();

    [AllowAnonymous]
    [HttpGet("terms")]
    public IActionResult Terms() => View();

    [AllowAnonymous]
    [HttpGet("child-data-policy")]
    public IActionResult ChildDataPolicy() => View();

    [AllowAnonymous]
    [Route("error/{code?}")]
    public IActionResult Error(int? code)
    {
        Response.StatusCode = code ?? 500;
        return View("NotFound");
    }
}
