using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class SystemMetricsDaily
{
    public int Id { get; set; }

    public DateOnly MetricDate { get; set; }

    public int ActiveParents { get; set; }

    public int ActiveChildren { get; set; }

    public long TotalMinutes { get; set; }

    public int LessonsCompleted { get; set; }

    public int OpenReports { get; set; }

    public int NewSources { get; set; }
}
