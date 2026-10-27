using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SafeLearnKids.Web.Areas.Kids.Controllers;

[Area("Kids")]
[Authorize(Roles = "Parent")]
public class LearnController : Controller
{
    [HttpGet("kids-picker")]
    public IActionResult Picker() => View();

    [HttpGet("kids-home")]
    public IActionResult Home() => View();

    [HttpGet("kids-subject")]
    public IActionResult Subject() => View();

    [HttpGet("kids-lesson")]
    public IActionResult Lesson() => View();

    [HttpGet("kids-reading")]
    public IActionResult Reading() => View();

    [HttpGet("kids-quiz")]
    public IActionResult Quiz() => View();

    [HttpGet("kids-result")]
    public IActionResult Result() => View();

    [HttpGet("kids-search")]
    public IActionResult Search() => View();

    [HttpGet("kids-time-up")]
    public IActionResult TimeUp() => View();

    [HttpGet("kids-badges")]
    public IActionResult Badges() => View();

}
