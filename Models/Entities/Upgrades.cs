namespace SafeLearnKids.Web.Models.Entities;

// Columns added after the scaffold; created by Services/SchemaUpdater.cs. Kept here so re-scaffolding does not drop them.

public partial class ChildProfile
{
    // The parent's switch: when false the child's search page is closed (checked on the server).
    public bool AllowSearch { get; set; } = true;
}

public partial class UserSetting
{
    // Wrong PINs or passwords at the child-mode exit; five in a row lock the exit until PinLockedUntil.
    public int PinFailedAttempts { get; set; }

    public DateTime? PinLockedUntil { get; set; }
}

public partial class ContentSource
{
    // Set when an admin asks for a sync, cleared by the background worker when it is done.
    public DateTime? SyncRequestedAt { get; set; }

    // The last outcome in words, for example "12 video, 3 mới chờ duyệt".
    public string? LastSyncResult { get; set; }
}

public partial class Question
{
    // "Explain it another way": a second, child-friendly explanation saved by an admin (hand-written or drafted by AI).
    public string? KidExplanation { get; set; }

    // "admin" when written by hand, otherwise the AI model that drafted it.
    public string? KidExplanationBy { get; set; }
}
