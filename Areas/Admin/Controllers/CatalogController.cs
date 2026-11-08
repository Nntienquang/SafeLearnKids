using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CatalogController(CatalogService catalog, AccountActivityLog activity, UserManager<ApplicationUser> users) : Controller
{
    [HttpGet("admin-catalog")]
    public async Task<IActionResult> Index(int? subject, int? grade, string? status, CancellationToken cancellationToken)
    {
        if (!catalog.Available) return View(new CatalogIndexViewModel { Available = false });
        var subjects = await catalog.SubjectsAsync(cancellationToken);
        var grades = await catalog.GradesAsync(cancellationToken);
        var selected = subjects.FirstOrDefault(item => item.Id == subject) ?? subjects.FirstOrDefault();
        if (grade is not null && grades.All(item => item.Id != grade)) grade = null;
        if (status is not null && !CatalogOptions.Statuses.Contains(status)) status = null;
        return View(new CatalogIndexViewModel
        {
            Subjects = subjects,
            Selected = selected,
            Grades = grades,
            Grade = grade,
            Status = status,
            Lessons = selected is null ? [] : await catalog.LessonsAsync(selected.Id, grade, status, cancellationToken)
        });
    }

    [HttpGet("admin-catalog/subjects/new")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (!catalog.Available) return RedirectToAction(nameof(Index));
        return View("SubjectForm", new SubjectFormViewModel { Grades = await catalog.GradesAsync(cancellationToken) });
    }

    [HttpPost("admin-catalog/subjects/new")]
    public async Task<IActionResult> Create(SubjectFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = null;
        if (ModelState.IsValid)
        {
            var result = await catalog.CreateAsync(model, cancellationToken);
            if (result.Id is { } created)
            {
                await activity.EntityActionAsync(ActorId, "CreateSubject", AccountActivityLog.SubjectEntity, created, model.Code.Trim().ToUpperInvariant());
                TempData["Info"] = "Đã thêm môn học.";
                return RedirectToAction(nameof(Index), new { subject = created });
            }
            ModelState.AddModelError(string.Empty, result.Error ?? "Chưa thể lưu môn học. Vui lòng thử lại.");
        }
        model.Grades = await catalog.GradesAsync(cancellationToken);
        return View("SubjectForm", model);
    }

    [HttpGet("admin-catalog/subjects/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        if (await catalog.FormAsync(id, cancellationToken) is not { } model) return NotFound();
        model.Grades = await catalog.GradesAsync(cancellationToken);
        return View("SubjectForm", model);
    }

    [HttpPost("admin-catalog/subjects/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, SubjectFormViewModel model, CancellationToken cancellationToken)
    {
        if (await catalog.FormAsync(id, cancellationToken) is not { } current) return NotFound();
        // The code never changes after creation, whatever the form posts.
        model.Id = id;
        model.Code = current.Code;
        model.IsActive = current.IsActive;
        model.Lessons = current.Lessons;
        ModelState.Remove(nameof(model.Code));
        if (ModelState.IsValid)
        {
            var result = await catalog.UpdateAsync(id, model, cancellationToken);
            if (result.NotFound) return NotFound();
            if (result.Error is null)
            {
                await activity.EntityActionAsync(ActorId, "UpdateSubject", AccountActivityLog.SubjectEntity, id, current.Code);
                TempData["Info"] = "Đã lưu môn học.";
                return RedirectToAction(nameof(Index), new { subject = id });
            }
            ModelState.AddModelError(string.Empty, result.Error);
        }
        model.Grades = await catalog.GradesAsync(cancellationToken);
        return View("SubjectForm", model);
    }

    [HttpPost("admin-catalog/subjects/{id:int}/visibility")]
    public async Task<IActionResult> Visibility(int id, bool active, CancellationToken cancellationToken)
    {
        var result = await catalog.SetActiveAsync(id, active, cancellationToken);
        if (result.NotFound) return NotFound();
        if (result.Error is null)
        {
            await activity.EntityActionAsync(ActorId, active ? "ShowSubject" : "HideSubject", AccountActivityLog.SubjectEntity, id);
            TempData["Info"] = active ? "Đã hiện môn học." : "Đã ẩn môn học.";
        }
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Index), new { subject = id });
    }

    [HttpPost("admin-catalog/subjects/{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var code = (await catalog.FormAsync(id, cancellationToken))?.Code;
        var result = await catalog.DeleteAsync(id, cancellationToken);
        if (result.NotFound) return NotFound();
        if (result.Error is not null)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index), new { subject = id });
        }
        await activity.EntityActionAsync(ActorId, "DeleteSubject", AccountActivityLog.SubjectEntity, id, code);
        TempData["Info"] = "Đã xóa môn học.";
        return RedirectToAction(nameof(Index));
    }

    // Kept for the sidebar link; lessons are opened from the catalog list.
    [HttpGet("admin-lesson")]
    public IActionResult Lesson() => RedirectToAction(nameof(Index));

    private string ActorId => users.GetUserId(User)!;
}
