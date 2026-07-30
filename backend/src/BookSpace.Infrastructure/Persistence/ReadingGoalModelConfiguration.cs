using BookSpace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookSpace.Infrastructure.Persistence;

public static class ReadingGoalModelConfiguration
{
    public static ModelBuilder ConfigureReadingGoals(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReadingGoal>(entity =>
        {
            entity.ToTable("reading_goals");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.Metric, x.StartDate, x.EndDate });
            entity.HasIndex(x => new { x.UserId, x.CompletedAt, x.EndDate });
            entity.Property(x => x.Metric).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Period).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
            entity.HasQueryFilter(x => x.DeletedAt == null);
        });

        return modelBuilder;
    }
}
