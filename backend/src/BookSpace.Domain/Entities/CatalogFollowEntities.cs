using BookSpace.Domain.Common;
using BookSpace.Domain.Exceptions;

namespace BookSpace.Domain.Entities;

public sealed class UserAuthorFollow : Entity
{
    private UserAuthorFollow() { }

    public UserAuthorFollow(Guid userId, Guid authorId)
    {
        UserId = EnsureId(userId, "CATALOG_FOLLOW_USER_REQUIRED", "Người dùng theo dõi không hợp lệ.");
        AuthorId = EnsureId(authorId, "CATALOG_FOLLOW_AUTHOR_REQUIRED", "Tác giả theo dõi không hợp lệ.");
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Guid AuthorId { get; private set; }
    public Author Author { get; private set; } = null!;

    public void Restore()
    {
        if (!DeletedAt.HasValue)
        {
            return;
        }

        DeletedAt = null;
        Touch();
    }

    private static Guid EnsureId(Guid id, string code, string message) =>
        id == Guid.Empty ? throw new DomainException(code, message) : id;
}

public sealed class UserCategoryFollow : Entity
{
    private UserCategoryFollow() { }

    public UserCategoryFollow(Guid userId, Guid categoryId)
    {
        UserId = EnsureId(userId, "CATALOG_FOLLOW_USER_REQUIRED", "Người dùng theo dõi không hợp lệ.");
        CategoryId = EnsureId(categoryId, "CATALOG_FOLLOW_CATEGORY_REQUIRED", "Thể loại theo dõi không hợp lệ.");
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Guid CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;

    public void Restore()
    {
        if (!DeletedAt.HasValue)
        {
            return;
        }

        DeletedAt = null;
        Touch();
    }

    private static Guid EnsureId(Guid id, string code, string message) =>
        id == Guid.Empty ? throw new DomainException(code, message) : id;
}
