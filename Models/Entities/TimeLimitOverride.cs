using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class TimeLimitOverride
{
    public int Id { get; set; }

    public int PolicyId { get; set; }

    public int Weekday { get; set; }

    public int Minutes { get; set; }

    public virtual TimeLimitPolicy Policy { get; set; } = null!;
}
