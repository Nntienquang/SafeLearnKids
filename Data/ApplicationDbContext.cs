using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SafeLearnKids.Web.Data;

// Domain entities (ChildProfile, Subject, Lesson, ContentItem, Quiz, TimeRequest, ContentReport...) are added here
// by the back-end sprints. The front end only depends on the ViewModels in /Models.
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }
}
