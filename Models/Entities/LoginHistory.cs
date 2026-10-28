using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class LoginHistory
{
    public long Id { get; set; }

    public string? UserId { get; set; }

    public string UserNameTried { get; set; } = null!;

    public bool Succeeded { get; set; }

    public string? FailReason { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? User { get; set; }
}
