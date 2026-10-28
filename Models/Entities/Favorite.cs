using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class Favorite
{
    public int Id { get; set; }

    public int ChildProfileId { get; set; }

    public int LessonId { get; set; }

    public DateTime FavoritedAt { get; set; }

    public virtual ChildProfile ChildProfile { get; set; } = null!;

    public virtual Lesson Lesson { get; set; } = null!;
}
