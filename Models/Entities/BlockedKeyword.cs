using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class BlockedKeyword
{
    public int Id { get; set; }

    public string Keyword { get; set; } = null!;

    public string? ParentId { get; set; }

    public string? CreatedById { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? CreatedBy { get; set; }

    public virtual User? Parent { get; set; }
}
