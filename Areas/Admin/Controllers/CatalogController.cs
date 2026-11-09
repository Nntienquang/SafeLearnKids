using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Admin.Controllers;

// The catalog lives in SQL business tables, so InMemory demo runs show a notice instead.
[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CatalogController(
    UserManager<ApplicationUser> users,
    AccountActivityLog activity,
    LessonFileStorage files,
    IServiceProvider services) : Controller
{
    private const int PageSize = 20;
    private const int MaxTags = 10;
    private const string SaveFailed = "Chưa thể lưu thay đổi. Vui lòng thử lại.";
    private const string Stale = "Bài học vừa được thay đổi ở nơi khác. Trang đã tải lại bản mới nhất, hãy kiểm tra rồi lưu lại.";

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();
    private string ActorId => users.GetUserId(User)!;

    [HttpGet("admin-catalog")]
    public async Task<IActionResult> Index(int? subject, int? grade, string? status, string? type, int? tag, string? q, int page = 1, CancellationToken cancellationToken = default)
    {
        if (Db is not { } db) return View("SqlOnly");
        var lessons = LessonQuery(db, subject, grade, status, type, tag, q);
        var matching = await lessons.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(matching / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);
        var rows = await lessons
            .OrderBy(lesson => lesson.Subject.SortOrder).ThenBy(lesson => lesson.GradeLevel.Level).ThenBy(lesson => lesson.SortOrder).ThenBy(lesson => lesson.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(lesson => new CatalogLessonRow(lesson.Id, lesson.Title, lesson.Subject.Name, lesson.GradeLevel.Level, lesson.LessonType, lesson.Status,
                lesson.LessonContents.Count, lesson.LessonTags.OrderBy(item => item.Tag.Name).Select(item => item.Tag.Name).ToList(), lesson.UpdatedAt))
            .ToListAsync(cancellationToken);
        return View(new CatalogIndexViewModel
        {
            Subject = subject,
            Grade = grade,
            Status = status,
            Type = type,
            Tag = tag,
            Q = q,
            Page = page,
            TotalPages = totalPages,
            Matching = matching,
            Subjects = await SubjectRowsAsync(db, cancellationToken),
            Tags = await TagsAsync(db, cancellationToken),
            Rows = rows
        });
    }

    [HttpGet("admin-catalog/subjects/new")]
    public IActionResult CreateSubject() => Db is null ? View("SqlOnly") : View("SubjectForm", new SubjectFormViewModel { Grades = [1, 2, 3, 4, 5] });

    [HttpPost("admin-catalog/subjects/new")]
    public async Task<IActionResult> CreateSubject(SubjectFormViewModel model, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        model.Id = null;
        var code = model.Code.Trim().ToUpperInvariant();
        await ValidateSubjectAsync(db, model, code, null, cancellationToken);
        if (!ModelState.IsValid) return View("SubjectForm", model);

        var subject = new Subject { Code = code };
        Apply(subject, model);
        var gradeIds = await GradeIdsAsync(db, model.Grades, cancellationToken);
        subject.SubjectGrades = gradeIds.Select(gradeId => new SubjectGrade { GradeLevelId = gradeId }).ToList();
        db.Subjects.Add(subject);
        if (!await SaveAsync(db, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Code), "Mã môn này đã có.");
            return View("SubjectForm", model);
        }
        await activity.ContentActionAsync(ActorId, "Subject", subject.Id, "CreateSubject", subject.Name);
        TempData["Info"] = "Đã thêm môn học.";
        return RedirectToAction(nameof(Index), new { subject = subject.Id });
    }

    [HttpGet("admin-catalog/subjects/{id:int}/edit")]
    public async Task<IActionResult> EditSubject(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var subject = await db.Subjects.AsNoTracking().Include(item => item.SubjectGrades).ThenInclude(item => item.GradeLevel)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (subject is null) return NotFound();
        return View("SubjectForm", new SubjectFormViewModel
        {
            Id = id,
            Name = subject.Name,
            Code = subject.Code,
            Description = subject.Description,
            IconKey = subject.IconKey ?? "book",
            ColorKey = subject.ColorKey ?? "blue",
            SortOrder = subject.SortOrder,
            IsActive = subject.IsActive,
            Grades = subject.SubjectGrades.Select(item => item.GradeLevel.Level).OrderBy(level => level).ToList(),
            Lessons = await db.Lessons.CountAsync(lesson => lesson.SubjectId == id, cancellationToken)
        });
    }

    [HttpPost("admin-catalog/subjects/{id:int}/edit")]
    public async Task<IActionResult> EditSubject(int id, SubjectFormViewModel model, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var subject = await db.Subjects.Include(item => item.SubjectGrades).ThenInclude(item => item.GradeLevel)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (subject is null) return NotFound();
        model.Id = id;
        // The code is the key children's profiles and pages use, so it never changes.
        model.Code = subject.Code;
        model.Lessons = await db.Lessons.CountAsync(lesson => lesson.SubjectId == id, cancellationToken);
        await ValidateSubjectAsync(db, model, subject.Code, id, cancellationToken);
        var dropped = subject.SubjectGrades.Where(item => !model.Grades.Contains(item.GradeLevel.Level)).ToList();
        var used = await db.Lessons.Where(lesson => lesson.SubjectId == id).Select(lesson => lesson.GradeLevelId).Distinct().ToListAsync(cancellationToken);
        var blocked = dropped.Where(item => used.Contains(item.GradeLevelId)).Select(item => item.GradeLevel.Level).OrderBy(level => level).ToList();
        if (blocked.Count > 0)
            ModelState.AddModelError(nameof(model.Grades), $"Lớp {string.Join(", ", blocked)} đang có bài học của môn này nên chưa bỏ được.");
        if (!ModelState.IsValid) return View("SubjectForm", model);

        Apply(subject, model);
        db.SubjectGrades.RemoveRange(dropped);
        var kept = subject.SubjectGrades.Select(item => item.GradeLevel.Level).ToHashSet();
        foreach (var gradeId in await GradeIdsAsync(db, model.Grades.Where(level => !kept.Contains(level)), cancellationToken))
            subject.SubjectGrades.Add(new SubjectGrade { GradeLevelId = gradeId });
        if (!await SaveAsync(db, cancellationToken))
        {
            TempData["Error"] = SaveFailed;
            return RedirectToAction(nameof(EditSubject), new { id });
        }
        await activity.ContentActionAsync(ActorId, "Subject", id, "UpdateSubject", subject.Name);
        TempData["Info"] = subject.IsActive ? "Đã lưu môn học." : "Đã lưu. Môn đang ẩn với phụ huynh và bé.";
        return RedirectToAction(nameof(Index), new { subject = id });
    }

    [HttpPost("admin-catalog/subjects/{id:int}/delete")]
    public async Task<IActionResult> DeleteSubject(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var subject = await db.Subjects.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (subject is null) return NotFound();
        var inUse = await db.Lessons.AnyAsync(item => item.SubjectId == id, cancellationToken)
            || await db.ChildSubjects.AnyAsync(item => item.SubjectId == id, cancellationToken)
            || await db.LearningPaths.AnyAsync(item => item.SubjectId == id, cancellationToken)
            || await db.SubjectProgresses.AnyAsync(item => item.SubjectId == id, cancellationToken);
        if (inUse)
        {
            TempData["Error"] = "Môn đã có bài học hoặc đang được bé học nên không xóa được. Hãy bỏ chọn \"Đang dùng\" để ẩn môn.";
            return RedirectToAction(nameof(EditSubject), new { id });
        }
        db.Subjects.Remove(subject);
        if (!await SaveAsync(db, cancellationToken))
        {
            TempData["Error"] = "Môn đang được dữ liệu khác sử dụng nên không xóa được. Hãy ẩn môn.";
            return RedirectToAction(nameof(EditSubject), new { id });
        }
        await activity.ContentActionAsync(ActorId, "Subject", id, "DeleteSubject", subject.Name);
        TempData["Info"] = "Đã xóa môn học.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("admin-lesson")]
    public IActionResult LessonStart() => RedirectToAction(nameof(CreateLesson));

    [HttpGet("admin-lesson/new")]
    public async Task<IActionResult> CreateLesson(int? subject, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var model = new LessonFormViewModel();
        await FillOptionsAsync(db, model, cancellationToken);
        var first = model.Subjects.FirstOrDefault(item => item.Id == subject) ?? model.Subjects.FirstOrDefault(item => item.IsActive && item.Grades.Count > 0);
        if (first is not null)
        {
            model.SubjectId = first.Id;
            model.Grade = first.Grades.FirstOrDefault();
        }
        return View("Lesson", model);
    }

    [HttpPost("admin-lesson/new")]
    public async Task<IActionResult> CreateLesson(LessonFormViewModel model, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        model.Id = null;
        var tags = ParseTags(model.Tags);
        var gradeId = await ValidateLessonAsync(db, model, tags, cancellationToken);
        if (!ModelState.IsValid || gradeId is null)
        {
            await FillOptionsAsync(db, model, cancellationToken);
            return View("Lesson", model);
        }
        var now = DateTime.UtcNow;
        var lesson = new Lesson { Status = LessonStatus.Draft, CreatedById = ActorId, CreatedAt = now };
        Apply(lesson, model, gradeId.Value, now);
        db.Lessons.Add(lesson);
        await SetTagsAsync(db, lesson, tags, cancellationToken);
        if (!await SaveAsync(db, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, SaveFailed);
            await FillOptionsAsync(db, model, cancellationToken);
            return View("Lesson", model);
        }
        await activity.ContentActionAsync(ActorId, "Lesson", lesson.Id, "CreateLesson", lesson.Title);
        TempData["Info"] = "Đã tạo bài học ở trạng thái nháp. Hãy thêm nội dung trước khi xuất bản.";
        return RedirectToAction(nameof(EditLesson), new { id = lesson.Id });
    }

    [HttpGet("admin-lesson/{id:int}")]
    public async Task<IActionResult> EditLesson(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var model = await LoadLessonAsync(db, id, cancellationToken);
        return model is null ? NotFound() : View("Lesson", model);
    }

    [HttpPost("admin-lesson/{id:int}")]
    public async Task<IActionResult> EditLesson(int id, LessonFormViewModel model, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var lesson = await LessonAccess.Standard(db).Include(item => item.LessonTags).ThenInclude(item => item.Tag).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lesson is null) return NotFound();
        model.Id = id;
        var tags = ParseTags(model.Tags);
        var gradeId = await ValidateLessonAsync(db, model, tags, cancellationToken);
        if (!ModelState.IsValid || gradeId is null)
        {
            var current = await LoadLessonAsync(db, id, cancellationToken);
            model.Status = current!.Status;
            model.Contents = current.Contents;
            model.PublishedAt = current.PublishedAt;
            model.ApprovedBy = current.ApprovedBy;
            await FillOptionsAsync(db, model, cancellationToken);
            return View("Lesson", model);
        }
        if (!Expect(db, lesson, model.RowVersion)) return Conflict(id);
        Apply(lesson, model, gradeId.Value, DateTime.UtcNow);
        await SetTagsAsync(db, lesson, tags, cancellationToken);
        return await FinishLessonAsync(db, lesson, "UpdateLesson", "Đã lưu bài học.", null, cancellationToken);
    }

    [HttpPost("admin-lesson/{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromForm] string? to, [FromForm] string? rowVersion, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var lesson = await LessonAccess.Standard(db).Include(item => item.Subject).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lesson is null) return NotFound();
        if (!Expect(db, lesson, rowVersion)) return Conflict(id);
        var allowed = lesson.Status switch
        {
            LessonStatus.Draft => new[] { LessonStatus.InReview, LessonStatus.Published, LessonStatus.Archived },
            LessonStatus.InReview => [LessonStatus.Draft, LessonStatus.Published, LessonStatus.Archived],
            LessonStatus.Published => [LessonStatus.Draft, LessonStatus.Archived],
            _ => [LessonStatus.Draft]
        };
        if (to is null || !allowed.Contains(to)) return Refuse(id, "Không thể chuyển bài học sang trạng thái này.");
        if (to == LessonStatus.Published && await PublishProblemAsync(db, lesson, cancellationToken) is { } problem) return Refuse(id, problem);

        lesson.Status = to;
        lesson.UpdatedAt = DateTime.UtcNow;
        if (to == LessonStatus.Published)
        {
            lesson.PublishedAt = lesson.UpdatedAt;
            lesson.ApprovedById = ActorId;
        }
        else
        {
            lesson.PublishedAt = null;
            lesson.ApprovedById = null;
        }
        var message = to switch
        {
            LessonStatus.Published => "Đã xuất bản. Phụ huynh đã thấy bài trong thư viện.",
            LessonStatus.InReview => "Đã gửi duyệt.",
            LessonStatus.Archived => "Đã lưu trữ. Bài không còn hiện với phụ huynh.",
            _ => "Đã đưa bài về bản nháp. Bài không còn hiện với phụ huynh."
        };
        return await FinishLessonAsync(db, lesson, "SetLessonStatus", message, to, cancellationToken);
    }

    [HttpPost("admin-lesson/{id:int}/delete")]
    public async Task<IActionResult> DeleteLesson(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var lesson = await LessonAccess.Standard(db).Include(item => item.LessonContents).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lesson is null) return NotFound();
        if (lesson.Status is LessonStatus.Published or LessonStatus.InReview)
            return Refuse(id, "Chỉ xóa được bài nháp hoặc đã lưu trữ. Hãy lưu trữ bài trước.");
        var urls = lesson.LessonContents.Select(item => item.FileUrl).ToList();
        db.Lessons.Remove(lesson);
        if (!await SaveAsync(db, cancellationToken))
            return Refuse(id, "Bài đã được giao, học hoặc dùng trong quiz nên không xóa được. Hãy giữ ở trạng thái lưu trữ.");
        urls.ForEach(files.Delete);
        await activity.ContentActionAsync(ActorId, "Lesson", id, "DeleteLesson", lesson.Title);
        TempData["Info"] = "Đã xóa bài học.";
        return RedirectToAction(nameof(Index), new { subject = lesson.SubjectId });
    }

    [HttpPost("admin-lesson/{id:int}/contents")]
    [RequestSizeLimit(LessonFileStorage.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = LessonFileStorage.MaxRequestBytes)]
    public async Task<IActionResult> AddContent(int id, LessonContentInput input, IFormFile? file, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var lesson = await LessonAccess.Standard(db).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lesson is null) return NotFound();
        if (!ModelState.IsValid) return Refuse(id, ModelState.Values.SelectMany(entry => entry.Errors).First().ErrorMessage);
        var title = string.IsNullOrWhiteSpace(input.Title) ? null : input.Title.Trim();
        var content = new LessonContent { LessonId = id, ContentKind = input.Kind, Title = title };
        switch (input.Kind)
        {
            case "Reading":
                if (string.IsNullOrWhiteSpace(input.Body)) return Refuse(id, "Nhập nội dung bài đọc.");
                content.Body = input.Body.Trim();
                break;
            case "Video":
                var video = await db.SourceVideos.AsNoTracking().Where(video => video.Id == input.VideoId).Where(video => video.Status == "Approved" && (!video.SourceVideoSources.Any() || video.SourceVideoSources.Any(link => link.Source.Status == "Approved"))).FirstOrDefaultAsync(cancellationToken);
                if (video is null) return Refuse(id, "Chọn một video đã được duyệt.");
                content.SourceVideoId = video.Id;
                content.Title ??= video.Title.Length > 150 ? video.Title[..150] : video.Title;
                break;
            case "Document" or "Image":
                var url = await files.SaveAsync(file, input.Kind, cancellationToken);
                if (url is null) return Refuse(id, LessonFileStorage.Rejected);
                content.FileUrl = url;
                var name = Path.GetFileName(file!.FileName);
                content.FileName = name.Length > 150 ? name[^150..] : name;
                break;
            default:
                return Refuse(id, "Loại nội dung không hợp lệ.");
        }
        content.SortOrder = (await db.LessonContents.Where(item => item.LessonId == id).MaxAsync(item => (int?)item.SortOrder, cancellationToken) ?? 0) + 1;
        db.LessonContents.Add(content);
        lesson.UpdatedAt = DateTime.UtcNow;
        if (!await SaveAsync(db, cancellationToken))
        {
            files.Delete(content.FileUrl);
            return Refuse(id, SaveFailed);
        }
        await activity.ContentActionAsync(ActorId, "Lesson", id, "AddLessonContent", LessonKind.ContentLabel(input.Kind));
        TempData["Info"] = "Đã thêm nội dung.";
        return RedirectToAction(nameof(EditLesson), new { id });
    }

    [HttpPost("admin-lesson/{id:int}/contents/{contentId:int}/move")]
    public async Task<IActionResult> MoveContent(int id, int contentId, [FromForm] bool up, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var items = await db.LessonContents.Where(item => item.LessonId == id).OrderBy(item => item.SortOrder).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        var index = items.FindIndex(item => item.Id == contentId);
        if (index < 0) return NotFound();
        var other = up ? index - 1 : index + 1;
        if (other >= 0 && other < items.Count)
        {
            (items[index], items[other]) = (items[other], items[index]);
            // Renumber so equal or missing orders from older data cannot stall a move.
            for (var i = 0; i < items.Count; i++) items[i].SortOrder = i + 1;
            await TouchAsync(db, id, cancellationToken);
            if (!await SaveAsync(db, cancellationToken)) return Refuse(id, SaveFailed);
        }
        return RedirectToAction(nameof(EditLesson), new { id });
    }

    [HttpPost("admin-lesson/{id:int}/contents/{contentId:int}/delete")]
    public async Task<IActionResult> DeleteContent(int id, int contentId, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var content = await db.LessonContents.FirstOrDefaultAsync(item => item.Id == contentId && item.LessonId == id, cancellationToken);
        if (content is null) return NotFound();
        var status = await LessonAccess.Standard(db).Where(item => item.Id == id).Select(item => item.Status).FirstAsync(cancellationToken);
        if (status == LessonStatus.Published && await db.LessonContents.CountAsync(item => item.LessonId == id, cancellationToken) == 1)
            return Refuse(id, "Bài đang xuất bản cần ít nhất 1 nội dung. Hãy thêm nội dung khác hoặc đưa bài về nháp trước.");
        db.LessonContents.Remove(content);
        await TouchAsync(db, id, cancellationToken);
        if (!await SaveAsync(db, cancellationToken)) return Refuse(id, SaveFailed);
        files.Delete(content.FileUrl);
        await activity.ContentActionAsync(ActorId, "Lesson", id, "RemoveLessonContent", LessonKind.ContentLabel(content.ContentKind));
        TempData["Info"] = "Đã xóa nội dung.";
        return RedirectToAction(nameof(EditLesson), new { id });
    }

    private static IQueryable<Lesson> LessonQuery(SafeLearnDbContext db, int? subject, int? grade, string? status, string? type, int? tag, string? q)
    {
        var query = LessonAccess.Standard(db).AsNoTracking();
        if (subject is not null) query = query.Where(lesson => lesson.SubjectId == subject);
        if (grade is not null) query = query.Where(lesson => lesson.GradeLevel.Level == grade);
        if (!string.IsNullOrEmpty(status)) query = query.Where(lesson => lesson.Status == status);
        if (!string.IsNullOrEmpty(type)) query = query.Where(lesson => lesson.LessonType == type);
        if (tag is not null) query = query.Where(lesson => lesson.LessonTags.Any(item => item.TagId == tag));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(lesson => lesson.Title.Contains(term) || (lesson.Summary != null && lesson.Summary.Contains(term)));
        }
        return query;
    }

    private static async Task<IReadOnlyList<CatalogSubjectRow>> SubjectRowsAsync(SafeLearnDbContext db, CancellationToken cancellationToken) =>
        await db.Subjects.AsNoTracking().OrderBy(subject => subject.SortOrder).ThenBy(subject => subject.Name)
            .Select(subject => new CatalogSubjectRow(subject.Id, subject.Code, subject.Name, subject.IconKey, subject.ColorKey, subject.IsActive,
                subject.Lessons.Count(lesson => lesson.OwnerParentId == null), subject.SubjectGrades.Select(item => item.GradeLevel.Level).OrderBy(level => level).ToList()))
            .ToListAsync(cancellationToken);

    private static async Task<IReadOnlyList<TagOption>> TagsAsync(SafeLearnDbContext db, CancellationToken cancellationToken) =>
        await db.Tags.AsNoTracking().OrderBy(tag => tag.Name).Select(tag => new TagOption(tag.Id, tag.Name)).ToListAsync(cancellationToken);

    private static Task<List<int>> GradeIdsAsync(SafeLearnDbContext db, IEnumerable<int> levels, CancellationToken cancellationToken)
    {
        var wanted = levels.Distinct().ToList();
        return db.GradeLevels.Where(grade => wanted.Contains(grade.Level)).Select(grade => grade.Id).ToListAsync(cancellationToken);
    }

    private async Task ValidateSubjectAsync(SafeLearnDbContext db, SubjectFormViewModel model, string code, int? id, CancellationToken cancellationToken)
    {
        model.Name = model.Name.Trim();
        model.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        model.Grades = model.Grades.Where(level => level is >= 1 and <= 5).Distinct().Order().ToList();
        if (model.Grades.Count == 0) ModelState.AddModelError(nameof(model.Grades), "Chọn ít nhất 1 lớp.");
        if (!SubjectCatalog.Colors.ContainsKey(model.ColorKey)) ModelState.AddModelError(nameof(model.ColorKey), "Chọn màu trong danh sách.");
        if (SubjectCatalog.Icons.All(item => item.Key != model.IconKey)) ModelState.AddModelError(nameof(model.IconKey), "Chọn biểu tượng trong danh sách.");
        if (id is null && ModelState.GetFieldValidationState(nameof(model.Code)) != Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Invalid)
        {
            if (SubjectCatalog.Reserved(code)) ModelState.AddModelError(nameof(model.Code), "Mã này trùng với khóa của môn có sẵn, hãy chọn mã khác.");
            else if (await db.Subjects.AnyAsync(subject => subject.Code == code, cancellationToken)) ModelState.AddModelError(nameof(model.Code), "Mã môn này đã có.");
        }
        if (await db.Subjects.AnyAsync(subject => subject.Name == model.Name && subject.Id != id, cancellationToken))
            ModelState.AddModelError(nameof(model.Name), "Đã có môn trùng tên.");
    }

    private static void Apply(Subject subject, SubjectFormViewModel model)
    {
        subject.Name = model.Name;
        subject.Description = model.Description;
        subject.IconKey = model.IconKey;
        subject.ColorKey = model.ColorKey;
        subject.SortOrder = model.SortOrder;
        subject.IsActive = model.IsActive;
    }

    private async Task<int?> ValidateLessonAsync(SafeLearnDbContext db, LessonFormViewModel model, IReadOnlyList<string> tags, CancellationToken cancellationToken)
    {
        model.Title = model.Title.Trim();
        model.Summary = string.IsNullOrWhiteSpace(model.Summary) ? null : model.Summary.Trim();
        if (!LessonKind.Types.Contains(model.LessonType)) ModelState.AddModelError(nameof(model.LessonType), "Chọn loại bài trong danh sách.");
        if (tags.Count > MaxTags) ModelState.AddModelError(nameof(model.Tags), $"Tối đa {MaxTags} nhãn.");
        if (tags.Any(tag => tag.Length > 40)) ModelState.AddModelError(nameof(model.Tags), "Mỗi nhãn tối đa 40 ký tự.");
        // A lesson must sit on a grade the subject is offered for (FK to dbo.SubjectGrades).
        var gradeId = await db.SubjectGrades.Where(item => item.SubjectId == model.SubjectId && item.GradeLevel.Level == model.Grade)
            .Select(item => (int?)item.GradeLevelId).FirstOrDefaultAsync(cancellationToken);
        if (gradeId is null && model.SubjectId > 0 && model.Grade > 0)
            ModelState.AddModelError(nameof(model.Grade), "Môn này chưa áp dụng cho lớp đã chọn. Hãy chọn lớp khác hoặc thêm lớp cho môn.");
        return gradeId;
    }

    private static void Apply(Lesson lesson, LessonFormViewModel model, int gradeId, DateTime now)
    {
        lesson.Title = model.Title;
        lesson.Summary = model.Summary;
        lesson.SubjectId = model.SubjectId;
        lesson.GradeLevelId = gradeId;
        lesson.LessonType = model.LessonType;
        lesson.DurationMin = model.DurationMin;
        lesson.SortOrder = model.SortOrder;
        lesson.UpdatedAt = now;
    }

    private static List<string> ParseTags(string? text) =>
        (text ?? "").Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(tag => string.Join(' ', tag.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .Where(tag => tag.Length > 0).DistinctBy(tag => tag.ToLowerInvariant()).ToList();

    // Keeps links that stay, so no row is deleted and re-inserted under the unique (LessonId, TagId) index.
    private static async Task SetTagsAsync(SafeLearnDbContext db, Lesson lesson, IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        bool Wanted(string name) => names.Any(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase));
        db.LessonTags.RemoveRange(lesson.LessonTags.Where(link => !Wanted(link.Tag.Name)).ToList());
        var missing = names.Where(name => lesson.LessonTags.All(link => !string.Equals(link.Tag?.Name, name, StringComparison.OrdinalIgnoreCase))).ToList();
        if (missing.Count == 0) return;
        var known = await db.Tags.Where(tag => missing.Contains(tag.Name)).ToListAsync(cancellationToken);
        foreach (var name in missing)
        {
            var tag = known.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) ?? new Tag { Name = name };
            lesson.LessonTags.Add(new LessonTag { Tag = tag });
        }
    }

    private static async Task<string?> PublishProblemAsync(SafeLearnDbContext db, Lesson lesson, CancellationToken cancellationToken)
    {
        if (!lesson.Subject.IsActive) return "Môn của bài đang ẩn. Hãy bật lại môn trước khi xuất bản.";
        var contents = await db.LessonContents.Where(item => item.LessonId == lesson.Id)
            .Select(item => new { item.ContentKind, Playable = item.SourceVideo != null && item.SourceVideo.Status == "Approved"
                && (!item.SourceVideo.SourceVideoSources.Any() || item.SourceVideo.SourceVideoSources.Any(link => link.Source.Status == "Approved")) }).ToListAsync(cancellationToken);
        if (contents.Count == 0) return "Cần ít nhất 1 nội dung trước khi xuất bản.";
        if (contents.Any(item => item.ContentKind == "Video" && !item.Playable))
            return "Bài có video chưa được duyệt, đã bị chặn hoặc thuộc nguồn bị tạm ngưng. Hãy gỡ video đó trước khi xuất bản.";
        return null;
    }

    private static bool Expect(SafeLearnDbContext db, Lesson lesson, string? rowVersion)
    {
        if (string.IsNullOrEmpty(rowVersion)) return false;
        try
        {
            db.Entry(lesson).Property(item => item.RowVersion).OriginalValue = Convert.FromBase64String(rowVersion);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static async Task TouchAsync(SafeLearnDbContext db, int id, CancellationToken cancellationToken)
    {
        var lesson = await LessonAccess.Standard(db).FirstAsync(item => item.Id == id, cancellationToken);
        lesson.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<IActionResult> FinishLessonAsync(SafeLearnDbContext db, Lesson lesson, string action, string message, string? detail, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(lesson.Id);
        }
        catch (DbUpdateException)
        {
            return Refuse(lesson.Id, SaveFailed);
        }
        await activity.ContentActionAsync(ActorId, "Lesson", lesson.Id, action, detail ?? lesson.Title);
        TempData["Info"] = message;
        return RedirectToAction(nameof(EditLesson), new { id = lesson.Id });
    }

    private static async Task<bool> SaveAsync(SafeLearnDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    private async Task<LessonFormViewModel?> LoadLessonAsync(SafeLearnDbContext db, int id, CancellationToken cancellationToken)
    {
        var lesson = await LessonAccess.Standard(db).AsNoTracking().Where(item => item.Id == id).Select(item => new
        {
            item.Id,
            item.Title,
            item.Summary,
            item.SubjectId,
            item.GradeLevel.Level,
            item.LessonType,
            item.DurationMin,
            item.SortOrder,
            item.Status,
            item.PublishedAt,
            item.RowVersion,
            ApprovedBy = item.ApprovedBy == null ? null : item.ApprovedBy.Email,
            Tags = item.LessonTags.OrderBy(tag => tag.Tag.Name).Select(tag => tag.Tag.Name).ToList(),
            Contents = item.LessonContents.OrderBy(content => content.SortOrder).ThenBy(content => content.Id).Select(content => new
            {
                content.Id,
                content.ContentKind,
                content.Title,
                content.Body,
                content.FileUrl,
                content.FileName,
                VideoTitle = content.SourceVideo == null ? null : content.SourceVideo.Title,
                VideoStatus = content.SourceVideo == null ? null : content.SourceVideo.Status == "Approved"
                    && (!content.SourceVideo.SourceVideoSources.Any() || content.SourceVideo.SourceVideoSources.Any(link => link.Source.Status == "Approved")) ? "Approved" : "Unavailable"
            }).ToList()
        }).FirstOrDefaultAsync(cancellationToken);
        if (lesson is null) return null;
        var model = new LessonFormViewModel
        {
            Id = lesson.Id,
            Title = lesson.Title,
            Summary = lesson.Summary,
            SubjectId = lesson.SubjectId,
            Grade = lesson.Level,
            LessonType = lesson.LessonType,
            DurationMin = lesson.DurationMin,
            SortOrder = lesson.SortOrder,
            Tags = string.Join(", ", lesson.Tags),
            RowVersion = Convert.ToBase64String(lesson.RowVersion),
            Status = lesson.Status,
            PublishedAt = lesson.PublishedAt,
            ApprovedBy = lesson.ApprovedBy,
            Contents = lesson.Contents.Select(content => new LessonContentRow(content.Id, content.ContentKind, content.Title,
                content.ContentKind switch
                {
                    "Reading" => content.Body is { Length: > 120 } body ? body[..120] + "…" : content.Body,
                    "Video" => content.VideoTitle,
                    _ => content.FileName
                }, content.FileUrl, content.VideoStatus)).ToList()
        };
        await FillOptionsAsync(db, model, cancellationToken);
        return model;
    }

    private static async Task FillOptionsAsync(SafeLearnDbContext db, LessonFormViewModel model, CancellationToken cancellationToken)
    {
        model.Subjects = await SubjectRowsAsync(db, cancellationToken);
        model.KnownTags = await TagsAsync(db, cancellationToken);
        model.Videos = await db.SourceVideos.AsNoTracking().Where(video => video.Status == "Approved" && (!video.SourceVideoSources.Any() || video.SourceVideoSources.Any(link => link.Source.Status == "Approved"))).OrderBy(video => video.Title)
            .Select(video => new VideoOption(video.Id, video.Title, video.DurationSec)).ToListAsync(cancellationToken);
    }

    private RedirectToActionResult Conflict(int id)
    {
        TempData["Error"] = Stale;
        return RedirectToAction(nameof(EditLesson), new { id });
    }

    private RedirectToActionResult Refuse(int id, string message)
    {
        TempData["Error"] = message;
        return RedirectToAction(nameof(EditLesson), new { id });
    }
}
