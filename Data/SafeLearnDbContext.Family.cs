using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Data;

// Mapping for family lessons, the community library and lesson requests. Kept out of the scaffolded file
// so re-scaffolding does not drop it; the columns and tables come from Services/SchemaUpdater.cs.
public partial class SafeLearnDbContext
{
    public virtual DbSet<LessonRequest> LessonRequests { get; set; }

    public virtual DbSet<LessonRequestVote> LessonRequestVotes { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.Property(e => e.ShareStatus).HasMaxLength(30);
            entity.Property(e => e.ShareNote).HasMaxLength(500);
            entity.HasIndex(e => e.OwnerParentId, "IX_Lessons_OwnerParentId");
            entity.HasOne(d => d.OwnerParent).WithMany().HasForeignKey(d => d.OwnerParentId).OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<LessonRequest>(entity =>
        {
            entity.ToTable("LessonRequests");
            entity.Property(e => e.Topic).HasMaxLength(200);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Reply).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.HasIndex(e => new { e.Status, e.SubjectId, e.GradeLevelId }, "IX_LessonRequests_Status_SubjectId_GradeLevelId");
            entity.HasOne(d => d.Parent).WithMany().HasForeignKey(d => d.ParentId);
            entity.HasOne(d => d.Subject).WithMany().HasForeignKey(d => d.SubjectId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.GradeLevel).WithMany().HasForeignKey(d => d.GradeLevelId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.Lesson).WithMany().HasForeignKey(d => d.LessonId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LessonRequestVote>(entity =>
        {
            entity.ToTable("LessonRequestVotes");
            entity.HasKey(e => new { e.RequestId, e.ParentId });
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.HasOne(d => d.Request).WithMany(p => p.Votes).HasForeignKey(d => d.RequestId);
        });
    }
}
