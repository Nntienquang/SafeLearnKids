using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Parent.Controllers;

[Area("Parent")]
[Authorize(Roles = "Parent")]
[RequestSizeLimit(16_384)]
[EnableRateLimiting("account")]
public class ChildrenController(UserManager<ApplicationUser> users, ChildProfileService children) : Controller
{
    [HttpGet("onboarding")]
    public IActionResult Onboarding() => View();

    [HttpGet("profiles")]
    public IActionResult Index() => View();

    [HttpPost("children")]
    public async Task<IActionResult> Create([FromBody] ChildProfileInput input, CancellationToken cancellationToken) =>
        ModelState.IsValid ? Respond(await children.CreateAsync(ParentId, input, cancellationToken)) : Invalid();

    [HttpPost("children/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ChildProfileInput input, CancellationToken cancellationToken) =>
        ModelState.IsValid ? Respond(await children.UpdateAsync(ParentId, id, input, cancellationToken)) : Invalid();

    [HttpPost("children/{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        Respond(await children.DeleteAsync(ParentId, id, cancellationToken));

    private string ParentId => users.GetUserId(User)!;

    private IActionResult Invalid() => BadRequest(new
    {
        error = ModelState.Values.SelectMany(entry => entry.Errors).Select(item => item.ErrorMessage).FirstOrDefault(message => message.Length > 0)
            ?? "Thông tin hồ sơ không hợp lệ."
    });

    private IActionResult Respond(ChildSaveResult result)
    {
        if (result.NotFound) return NotFound();
        if (result.Error is not null) return Conflict(new { error = result.Error });
        return result.Child is null ? NoContent() : Json(result.Child);
    }
}
