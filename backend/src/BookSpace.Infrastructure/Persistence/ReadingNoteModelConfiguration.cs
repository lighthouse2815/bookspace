using BookSpace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookSpace.Infrastructure.Persistence;

public static class ReadingNoteModelConfiguration
{
    /// <summary>
    /// Adds the ReadingNote entity to the BookSpace EF model. The composition
    /// root should invoke this from BookSpaceDbContext.OnModelCreating.
    /// </summary>
    public static ModelBuilder ConfigureReadingNotes(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReadingNote>(entity =>
        {
            entity.ToTable("reading_notes");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.BookId, x.CreatedAt });
            entity.HasIndex(x => new { x.UserId, x.UpdatedAt });
            entity.Property(x => x.Quote).HasMaxLength(500);
            entity.Property(x => x.Content).HasMaxLength(5000);
            entity.Property(x => x.TagsCsv).HasMaxLength(500);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Book).WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.IsDeleted);
            entity.HasQueryFilter(x => x.DeletedAt == null);
        });

        return modelBuilder;
    }
}
