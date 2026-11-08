using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class LessonsController(
    LessonService lessons,
    LessonContentService contents,
    AccountActivityLog activity,
    UserManager<ApplicationUser> users) : Controller
{
    [HttpGet("admin-lessons/new")]
    public async Task<IActionResult> Create(int subject, CancellationToken cancellationToken)
    {
        if (!lessons.Available) return Unavailable();
        return await lessons.NewFormAsync(subject, cancellationToken) is { } form ? View(form) : NotFound();
    }

    [HttpPost("admin-lessons/new")]
    public async Task<IActionResult> Create(LessonFormViewModel model, CancellationToken cancellationToken)
    {
        if (!lessons.Available) return Unavailable();
        if (await lessons.NewFormAsync(model.SubjectId, cancellationToken) is not { } blank) return NotFound();
        model.Id = null;
        if (ModelState.IsValid)
        {
            var result = await lessons.CreateAsync(model, ActorId, cancellationToken);
            if (result.Id is { } created)
            {
                await AuditAsync("CreateLesson", created, model.Title.Trim());
                TempData["Info"] = "Đã tạo bài nháp.";
                return RedirectToAction(nameof(Edit), new { id = created });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        model.SubjectName = blank.SubjectName;
        model.Grades = blank.Grades;
        return View(model);
    }

    [HttpGet("admin-lessons/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        if (!lessons.Available) return Unavailable();
        return await EditorAsync(id, cancellationToken) is { } editor ? View(editor) : NotFound();
    }

    [HttpPost("admin-lessons/{id:int}")]
    public async Task<IActionResult> Edit(int id, LessonFormViewModel model, CancellationToken cancellationToken)
    {
        if (!lessons.Available) return Unavailable();
        if (await EditorAsync(id, cancellationToken) is not { } editor) return NotFound();
        if (ModelState.IsValid)
        {
            var result = await lessons.UpdateAsync(id, model, cancellationToken);
            if (result.NotFound) return NotFound();
            if (result.Error is null)
            {
                await AuditAsync("UpdateLesson", id, model.Title.Trim());
                TempData["Info"] = "Đã lưu thông tin bài.";
                return RedirectToAction(nameof(Edit), new { id });
            }
            ModelState.AddModelError(string.Empty, result.Error);
        }
        // Show what the admin typed, keeping the posted RowVersion so a stale page cannot be saved over newer data.
        model.Id = id;
        model.SubjectId = editor.Form.SubjectId;
        model.SubjectName = editor.Form.SubjectName;
        model.Grades = editor.Form.Grades;
        editor.Form = model;
        return View(editor);
    }

    [HttpPost("admin-lessons/{id:int}/contents")]
    public async Task<IActionResult> AddContent(int id, LessonContentInput input, CancellationToken cancellationToken)
    {
        input.Id = null;
        var result = await contents.AddAsync(id, input, cancellationToken);
        return await ContentResultAsync(id, input, result, "AddLessonContent", "Đã thêm nội dung.", cancellationToken);
    }

    [HttpPost("admin-lessons/{id:int}/contents/{contentId:int}")]
    public async Task<IActionResult> UpdateContent(int id, int contentId, LessonContentInput input, CancellationToken cancellationToken)
    {
        input.Id = contentId;
        var result = await contents.UpdateAsync(id, input, cancellationToken);
        return await ContentResultAsync(id, input, result, "UpdateLessonContent", "Đã lưu nội dung.", cancellationToken);
    }

    [HttpPost("admin-lessons/{id:int}/contents/{contentId:int}/move")]
    public async Task<IActionResult> MoveContent(int id, int contentId, string? rowVersion, string? direction, CancellationToken cancellationToken)
    {
        var result = await contents.MoveAsync(id, contentId, rowVersion, direction == "up", cancellationToken);
        return await ButtonResultAsync(id, result, "MoveLessonContent", $"#{contentId} {direction}", "Đã đổi thứ tự.");
    }

    [HttpPost("admin-lessons/{id:int}/contents/{contentId:int}/delete")]
    public async Task<IActionResult> DeleteContent(int id, int contentId, string? rowVersion, CancellationToken cancellationToken)
    {
        var result = await contents.DeleteAsync(id, contentId, rowVersion, cancellationToken);
        return await ButtonResultAsync(id, result, "DeleteLessonContent", $"#{contentId}", "Đã xóa nội dung.");
    }

    [HttpPost("admin-lessons/{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, string? target, string? rowVersion, CancellationToken cancellationToken)
    {
        var result = await lessons.ChangeStatusAsync(id, target, rowVersion, ActorId, cancellationToken);
        var message = target switch
        {
            LessonWorkflow.Published => "Đã xuất bản bài.",
            LessonWorkflow.Archived => "Đã lưu trữ bài.",
            _ => "Đã đưa bài về Bản nháp."
        };
        return await ButtonResultAsync(id, result, "ChangeLessonStatus", result.Detail, message);
    }

    [HttpPost("admin-lessons/{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, string? rowVersion, CancellationToken cancellationToken)
    {
        if (!lessons.Available) return Unavailable();
        if (await lessons.EditorAsync(id, cancellationToken) is not { } editor) return NotFound();
        var result = await lessons.DeleteAsync(id, rowVersion, cancellationToken);
        if (result.Error is not null) return await ButtonResultAsync(id, result, "DeleteLesson", null, string.Empty);
        await AuditAsync("DeleteLesson", id, result.Detail);
        TempData["Info"] = "Đã xóa bài học.";
        return RedirectToAction("Index", "Catalog", new { subject = editor.Form.SubjectId });
    }

    private async Task<LessonEditorViewModel?> EditorAsync(int id, CancellationToken cancellationToken)
    {
        if (await lessons.EditorAsync(id, cancellationToken) is not { } editor) return null;
        editor.Contents = await contents.ContentsAsync(id, cancellationToken);
        editor.Videos = await contents.ApprovedVideosAsync(cancellationToken);
        return editor;
    }

    // Typed parts come back on the page when saving fails, so nothing the admin wrote is lost.
    private async Task<IActionResult> ContentResultAsync(
        int id, LessonContentInput input, LessonSaveResult result, string action, string message, CancellationToken cancellationToken)
    {
        if (!lessons.Available) return Unavailable();
        if (result.NotFound) return NotFound();
        if (result.Error is null)
        {
            await AuditAsync(action, id, $"{input.ContentKind} {input.Title?.Trim()}".Trim());
            TempData["Info"] = message;
            return RedirectToAction(nameof(Edit), new { id });
        }
        if (await EditorAsync(id, cancellationToken) is not { } editor) return NotFound();
        editor.FailedContent = input;
        editor.ContentError = result.Error;
        return View(nameof(Edit), editor);
    }

    private async Task<IActionResult> ButtonResultAsync(int id, LessonSaveResult result, string action, string? detail, string message)
    {
        if (!lessons.Available) return Unavailable();
        if (result.NotFound) return NotFound();
        if (result.Error is null)
        {
            await AuditAsync(action, id, detail);
            TempData["Info"] = message;
        }
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Edit), new { id });
    }

    private Task AuditAsync(string action, int lessonId, string? detail) =>
        activity.EntityActionAsync(ActorId, action, AccountActivityLog.LessonEntity, lessonId, detail);

    private IActionResult Unavailable()
    {
        TempData["Error"] = "Bài học chỉ có khi chạy với SQL Server.";
        return RedirectToAction("Index", "Catalog");
    }

    private string ActorId => users.GetUserId(User)!;
}
