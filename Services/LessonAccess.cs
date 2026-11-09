using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

// Who may see a lesson. Three layers:
//   standard library  OwnerParentId is null                 written by admins, everyone
//   family lesson     OwnerParentId is the parent          only that family
//   community lesson  family lesson with ShareStatus Approved   everyone, after an admin reviewed it
public static class LessonAccess
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    // Lessons every family may see (before status and subject checks).
    public static IQueryable<Lesson> Public(IQueryable<Lesson> lessons) =>
        lessons.Where(lesson => lesson.OwnerParentId == null || lesson.ShareStatus == Approved);

    // Lessons this family may see: the public ones plus its own.
    public static IQueryable<Lesson> ForFamily(IQueryable<Lesson> lessons, string parentId) =>
        lessons.Where(lesson => lesson.OwnerParentId == null || lesson.ShareStatus == Approved || lesson.OwnerParentId == parentId);

    // The admins' catalog: only lessons they write themselves.
    public static IQueryable<Lesson> Standard(SafeLearnDbContext db) => db.Lessons.Where(lesson => lesson.OwnerParentId == null);

    public static string ShareLabel(string? status) => status switch
    {
        Pending => "Đang chờ duyệt chia sẻ",
        Approved => "Đã chia sẻ lên cộng đồng",
        Rejected => "Chưa được duyệt chia sẻ",
        _ => "Chỉ gia đình"
    };

    public static string ShareTone(string? status) => status switch
    {
        Pending => "t-a",
        Approved => "t-g",
        Rejected => "t-r",
        _ => "t-n"
    };
}
