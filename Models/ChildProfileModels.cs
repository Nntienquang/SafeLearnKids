using System.ComponentModel.DataAnnotations;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Models;

// ParentId is never bound from a request; it always comes from the signed-in user.
public class ChildProfileInput
{
    [Required(ErrorMessage = "Nhập tên của bé.")]
    [StringLength(30, ErrorMessage = "Tên tối đa 30 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Range(1, 5, ErrorMessage = "Lớp không hợp lệ.")]
    public int Cls { get; set; }

    public int? BirthYear { get; set; }

    public List<string> Subjects { get; set; } = [];

    // US-10: required when a profile is created; ignored on later edits (consent is managed on the privacy page).
    public bool Consent { get; set; }
}

// Matches the child shape store.js keeps, so demo pages read SQL profiles unchanged.
// Limit and Used are today's minutes from the server (Limit includes extra time granted today).
public record ParentChildRow(string Id, string Name, int Cls, int? BirthYear, string Init, IReadOnlyList<string> Subjects, int Limit = StudyTimeService.DefaultDailyMinutes, int Used = 0);

public record ChildSaveResult(ParentChildRow? Child, string? Error, bool NotFound = false);
