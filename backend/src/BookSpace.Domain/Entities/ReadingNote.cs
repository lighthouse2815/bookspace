using BookSpace.Domain.Common;
using BookSpace.Domain.Exceptions;

namespace BookSpace.Domain.Entities;

/// <summary>
/// Ghi chú riêng tư của một người dùng khi đọc một cuốn sách.
/// </summary>
public sealed class ReadingNote : Entity
{
    private ReadingNote() { }

    public ReadingNote(
        Guid userId,
        Guid bookId,
        int? pageNumber,
        string? quote,
        string? content,
        IEnumerable<string>? tags)
    {
        UserId = userId;
        BookId = bookId;
        Apply(pageNumber, quote, content, tags, touch: false);
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Guid BookId { get; private set; }
    public Book Book { get; private set; } = null!;
    public int? PageNumber { get; private set; }
    public string? Quote { get; private set; }
    public string? Content { get; private set; }
    public string? TagsCsv { get; private set; }

    public IReadOnlyList<string> Tags => string.IsNullOrWhiteSpace(TagsCsv)
        ? Array.Empty<string>()
        : TagsCsv.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void Update(
        int? pageNumber,
        string? quote,
        string? content,
        IEnumerable<string>? tags) =>
        Apply(pageNumber, quote, content, tags, touch: true);

    public static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return Array.Empty<string>();
        }

        var normalized = new List<string>();
        foreach (var rawTag in tags)
        {
            var tag = rawTag?.Trim();
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            if (tag.Contains('|', StringComparison.Ordinal))
            {
                throw new DomainException("INVALID_READING_NOTE_TAG", "Thẻ ghi chú không được chứa ký tự |.");
            }

            if (tag.Length > 30)
            {
                throw new DomainException("INVALID_READING_NOTE_TAG", "Mỗi thẻ ghi chú không được vượt quá 30 ký tự.");
            }

            if (!normalized.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                normalized.Add(tag);
            }
        }

        if (normalized.Count > 10)
        {
            throw new DomainException("INVALID_READING_NOTE_TAG", "Một ghi chú chỉ được có tối đa 10 thẻ.");
        }

        var tagsCsv = string.Join('|', normalized);
        if (tagsCsv.Length > 500)
        {
            throw new DomainException("INVALID_READING_NOTE_TAG", "Tổng độ dài các thẻ không được vượt quá 500 ký tự.");
        }

        return normalized;
    }

    private void Apply(
        int? pageNumber,
        string? quote,
        string? content,
        IEnumerable<string>? tags,
        bool touch)
    {
        if (pageNumber is <= 0)
        {
            throw new DomainException("INVALID_NOTE_PAGE_NUMBER", "Số trang ghi chú phải lớn hơn 0.");
        }

        var normalizedQuote = Guard.Optional(quote, "Trích dẫn", 500);
        var normalizedContent = Guard.Optional(content, "Nội dung ghi chú", 5000);
        if (normalizedQuote is null && normalizedContent is null)
        {
            throw new DomainException(
                "READING_NOTE_CONTENT_REQUIRED",
                "Ghi chú cần có ít nhất một trích dẫn hoặc nội dung.");
        }

        var normalizedTags = NormalizeTags(tags);
        PageNumber = pageNumber;
        Quote = normalizedQuote;
        Content = normalizedContent;
        TagsCsv = normalizedTags.Count == 0 ? null : string.Join('|', normalizedTags);

        if (touch)
        {
            Touch();
        }
    }
}
