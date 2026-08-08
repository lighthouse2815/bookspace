using BookSpace.Application.Abstractions;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;

namespace BookSpace.Application.Services;

public sealed class CatalogFollowingService(IBookSpaceDbContext db) : ICatalogFollowingService
{
    private readonly ServiceMapper _mapper = new(db);

    public CatalogFollowingDto GetMine(Guid userId)
    {
        var authors = db.Authors
            .Where(author => db.UserAuthorFollows.Any(link =>
                link.UserId == userId && link.AuthorId == author.Id))
            .OrderBy(author => author.Name)
            .ThenBy(author => author.Id)
            .ToList()
            .Select(_mapper.Author)
            .ToList();
        var categories = db.Categories
            .Where(category => db.UserCategoryFollows.Any(link =>
                link.UserId == userId && link.CategoryId == category.Id))
            .OrderBy(category => category.Name)
            .ThenBy(category => category.Id)
            .ToList()
            .Select(_mapper.Category)
            .ToList();

        return new CatalogFollowingDto(authors, categories);
    }

    public async Task FollowAuthorAsync(
        Guid userId,
        Guid authorId,
        CancellationToken cancellationToken)
    {
        if (!db.Authors.Any(author => author.Id == authorId))
        {
            throw ServiceErrors.NotFound("AUTHOR_NOT_FOUND", "Không tìm thấy tác giả.");
        }

        var existing = db.UserAuthorFollowsIncludingDeleted.FirstOrDefault(link =>
            link.UserId == userId && link.AuthorId == authorId);
        if (existing is null)
        {
            db.Add(new UserAuthorFollow(userId, authorId));
        }
        else if (existing.DeletedAt.HasValue)
        {
            existing.Restore();
        }
        else
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnfollowAuthorAsync(
        Guid userId,
        Guid authorId,
        CancellationToken cancellationToken)
    {
        var existing = db.UserAuthorFollows.FirstOrDefault(link =>
            link.UserId == userId && link.AuthorId == authorId);
        if (existing is null)
        {
            return;
        }

        existing.SoftDelete();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FollowCategoryAsync(
        Guid userId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        if (!db.Categories.Any(category => category.Id == categoryId))
        {
            throw ServiceErrors.NotFound("CATEGORY_NOT_FOUND", "Không tìm thấy thể loại.");
        }

        var existing = db.UserCategoryFollowsIncludingDeleted.FirstOrDefault(link =>
            link.UserId == userId && link.CategoryId == categoryId);
        if (existing is null)
        {
            db.Add(new UserCategoryFollow(userId, categoryId));
        }
        else if (existing.DeletedAt.HasValue)
        {
            existing.Restore();
        }
        else
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnfollowCategoryAsync(
        Guid userId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var existing = db.UserCategoryFollows.FirstOrDefault(link =>
            link.UserId == userId && link.CategoryId == categoryId);
        if (existing is null)
        {
            return;
        }

        existing.SoftDelete();
        await db.SaveChangesAsync(cancellationToken);
    }
}
