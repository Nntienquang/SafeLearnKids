using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class ChildSubject
{
    public int Id { get; set; }

    public int ChildProfileId { get; set; }

    public int SubjectId { get; set; }

    public virtual ChildProfile ChildProfile { get; set; } = null!;

    public virtual Subject Subject { get; set; } = null!;
}
