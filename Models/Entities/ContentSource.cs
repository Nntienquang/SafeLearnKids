using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class ContentSource
{
    public int Id { get; set; }

    public string Platform { get; set; } = null!;

    public string ExternalId { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Url { get; set; } = null!;

    public string? Description { get; set; }

    public string Status { get; set; } = null!;

    public string? SuggestedById { get; set; }

    public string? ReviewedById { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewNote { get; set; }

    public DateTime? LastSyncedAt { get; set; }

    public string? LastSyncError { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<CurationSuggestion> CurationSuggestions { get; set; } = new List<CurationSuggestion>();

    public virtual ICollection<ParentSource> ParentSources { get; set; } = new List<ParentSource>();

    public virtual User? ReviewedBy { get; set; }

    public virtual ICollection<SourceVideoSource> SourceVideoSources { get; set; } = new List<SourceVideoSource>();

    public virtual User? SuggestedBy { get; set; }
}
