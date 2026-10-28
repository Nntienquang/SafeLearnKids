using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class Streak
{
    public int Id { get; set; }

    public int ChildProfileId { get; set; }

    public int CurrentDays { get; set; }

    public int LongestDays { get; set; }

    public DateOnly? LastActiveDate { get; set; }

    public virtual ChildProfile ChildProfile { get; set; } = null!;
}
