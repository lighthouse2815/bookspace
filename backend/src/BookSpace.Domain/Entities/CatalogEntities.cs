using BookSpace.Domain.Common;

namespace BookSpace.Domain.Entities;

public sealed class Author : Entity
{
    private Author() { }

    public Author(string name, string? biography = null, string? avatarUrl = null)
    {
        Name = Guard.Required(name, "Tên tác giả", 200);
        Biography = Guard.Optional(biography, "Tiểu sử tác giả", 2000);
        AvatarUrl = Guard.Optional(avatarUrl, "Ảnh tác giả", 1000);
    }

    public string Name { get; private set; } = string.Empty;
    public string? Biography { get; private set; }
    public string? AvatarUrl { get; private set; }
    public ICollection<BookAuthor> Books { get; } = new List<BookAuthor>();

    public void Update(string name, string? biography, string? avatarUrl)
    {
        Name = Guard.Required(name, "Tên tác giả", 200);
        Biography = Guard.Optional(biography, "Tiểu sử tác giả", 2000);
        AvatarUrl = Guard.Optional(avatarUrl, "Ảnh tác giả", 1000);
        Touch();
    }
}

public sealed class Category : Entity
{
    private Category() { }

    public Category(string name, string? description = null)
    {
        Name = Guard.Required(name, "Tên thể loại", 100);
        Description = Guard.Optional(description, "Mô tả thể loại", 500);
    }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public ICollection<BookCategory> Books { get; } = new List<BookCategory>();

    public void Update(string name, string? description)
    {
        Name = Guard.Required(name, "Tên thể loại", 100);
        Description = Guard.Optional(description, "Mô tả thể loại", 500);
        Touch();
    }
}

public sealed class Book : Entity
{
    private Book() { }

    public Book(
        string title,
        string? description,
        string? isbn,
        string? coverUrl,
        int pageCount,
        int? publicationYear,
        string language = "vi")
    {
        Update(title, description, isbn, coverUrl, pageCount, publicationYear, language);
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = null;
    }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Isbn { get; private set; }
    public string? CoverUrl { get; private set; }
    public int PageCount { get; private set; }
    public int? PublicationYear { get; private set; }
    public string Language { get; private set; } = "vi";
    public ICollection<BookAuthor> Authors { get; } = new List<BookAuthor>();
    public ICollection<BookCategory> Categories { get; } = new List<BookCategory>();

    public void Update(
        string title,
        string? description,
        string? isbn,
        string? coverUrl,
        int pageCount,
        int? publicationYear,
        string language)
    {
        Title = Guard.Required(title, "Tên sách", 300);
        Description = Guard.Optional(description, "Mô tả sách", 5000);
        Isbn = Guard.Optional(isbn, "ISBN", 20);
        CoverUrl = Guard.Optional(coverUrl, "Ảnh bìa", 1000);
        PageCount = Guard.Positive(pageCount, "Số trang");
        if (publicationYear is < 1000 || publicationYear > 2200)
        {
            throw new Exceptions.DomainException("INVALID_PUBLICATION_YEAR", "Năm xuất bản không hợp lệ.");
        }

        PublicationYear = publicationYear;
        Language = Guard.Required(language, "Ngôn ngữ", 20).ToLowerInvariant();
        Touch();
    }
}

public sealed class BookAuthor : Entity
{
    private BookAuthor() { }
    public BookAuthor(Guid bookId, Guid authorId)
    {
        BookId = bookId;
        AuthorId = authorId;
    }

    public Guid BookId { get; private set; }
    public Book Book { get; private set; } = null!;
    public Guid AuthorId { get; private set; }
    public Author Author { get; private set; } = null!;
}

public sealed class BookCategory : Entity
{
    private BookCategory() { }
    public BookCategory(Guid bookId, Guid categoryId)
    {
        BookId = bookId;
        CategoryId = categoryId;
    }

    public Guid BookId { get; private set; }
    public Book Book { get; private set; } = null!;
    public Guid CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;
}

public sealed class ExternalBookLink : Entity
{
    private ExternalBookLink() { }

    public ExternalBookLink(string provider, string externalId, Guid bookId)
    {
        Provider = Guard.Required(provider, "Nhà cung cấp", 50).ToLowerInvariant();
        ExternalId = Guard.Required(externalId, "Mã sách bên ngoài", 200);
        BookId = bookId;
    }

    public string Provider { get; private set; } = string.Empty;
    public string ExternalId { get; private set; } = string.Empty;
    public Guid BookId { get; private set; }
    public Book Book { get; private set; } = null!;
}
