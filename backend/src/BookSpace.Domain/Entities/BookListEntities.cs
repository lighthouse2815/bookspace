using BookSpace.Domain.Common;
using BookSpace.Domain.Enums;
using BookSpace.Domain.Exceptions;

namespace BookSpace.Domain.Entities;

public sealed class BookList : Entity
{
    private BookList() { }

    public BookList(
        Guid ownerId,
        string name,
        string? description,
        BookListVisibility visibility)
    {
        if (ownerId == Guid.Empty)
        {
            throw new DomainException("VALIDATION_ERROR", "Chủ bộ sưu tập không hợp lệ.");
        }

        OwnerId = ownerId;
        Update(name, description, visibility);
        UpdatedAt = null;
    }

    public Guid OwnerId { get; private set; }
    public User Owner { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public BookListVisibility Visibility { get; private set; }
    public ICollection<BookListItem> Items { get; } = new List<BookListItem>();

    public void Update(
        string name,
        string? description,
        BookListVisibility visibility)
    {
        if (!Enum.IsDefined(visibility))
        {
            throw new DomainException(
                "INVALID_BOOK_LIST_VISIBILITY",
                "Quyền riêng tư của bộ sưu tập không hợp lệ.");
        }

        Name = Guard.Required(name, "Tên bộ sưu tập", 120);
        NormalizedName = Name.ToUpperInvariant();
        Description = Guard.Optional(description, "Mô tả bộ sưu tập", 1000);
        Visibility = visibility;
        Touch();
    }

    public void MarkItemsChanged() => Touch();
}

public sealed class BookListItem : Entity
{
    private BookListItem() { }

    public BookListItem(Guid bookListId, Guid bookId, int position)
    {
        if (bookListId == Guid.Empty || bookId == Guid.Empty)
        {
            throw new DomainException("VALIDATION_ERROR", "Sách hoặc bộ sưu tập không hợp lệ.");
        }

        BookListId = bookListId;
        BookId = bookId;
        Position = ValidatePosition(position);
    }

    public Guid BookListId { get; private set; }
    public BookList BookList { get; private set; } = null!;
    public Guid BookId { get; private set; }
    public Book Book { get; private set; } = null!;
    public int Position { get; private set; }

    public void MoveTo(int position)
    {
        var normalizedPosition = ValidatePosition(position);
        if (Position == normalizedPosition)
        {
            return;
        }

        Position = normalizedPosition;
        Touch();
    }

    public void Restore(int position)
    {
        if (!DeletedAt.HasValue)
        {
            throw new DomainException(
                "BOOK_ALREADY_IN_LIST",
                "Sách đã có trong bộ sưu tập này.");
        }

        DeletedAt = null;
        Position = ValidatePosition(position);
        Touch();
    }

    private static int ValidatePosition(int position)
    {
        if (position < 0)
        {
            throw new DomainException(
                "INVALID_BOOK_LIST_POSITION",
                "Vị trí sách trong bộ sưu tập không hợp lệ.");
        }

        return position;
    }
}
