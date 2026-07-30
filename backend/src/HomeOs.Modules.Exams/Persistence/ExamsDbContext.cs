using HomeOs.Modules.Exams.Domain;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Exams.Persistence;

/// <summary>EF Core context owning the Exams module's tables (attempts and their answers).</summary>
public sealed class ExamsDbContext(DbContextOptions<ExamsDbContext> options) : DbContext(options)
{
    public DbSet<ExamAttempt> Attempts => Set<ExamAttempt>();
    public DbSet<ExamAnswer> Answers => Set<ExamAnswer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExamAttempt>(e =>
        {
            e.ToTable("ExamAttempts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Laws).HasMaxLength(200).IsRequired();
            e.Property(x => x.Mode).HasMaxLength(20).IsRequired();
            e.Property(x => x.EarnedPoints).HasPrecision(8, 2);
            e.Property(x => x.MaxPoints).HasPrecision(8, 2);
            e.HasMany(x => x.Answers).WithOne().HasForeignKey(a => a.AttemptId).OnDelete(DeleteBehavior.Cascade);
            // The history screen reads "my attempts, newest first".
            e.HasIndex(x => new { x.HouseholdId, x.MemberId, x.StartedAtUtc });
        });

        modelBuilder.Entity<ExamAnswer>(e =>
        {
            e.ToTable("ExamAnswers");
            e.HasKey(x => x.Id);
            e.Property(x => x.QuestionId).HasMaxLength(40).IsRequired();
            e.Property(x => x.Given).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Feedback).HasMaxLength(1000);
            e.Property(x => x.Points).HasPrecision(6, 2);
            e.Property(x => x.MaxPoints).HasPrecision(6, 2);
            e.HasIndex(x => new { x.AttemptId, x.Ordinal });
        });
    }
}
