using BookSpace.Domain.Common;

namespace BookSpace.Domain.Entities;

public sealed class Review : Entity
{
    private Review() { }

    public Review(Guid userId, Guid bookId, int rating, string content, bool containsSpoilers)
    {
        UserId = userId;
        BookId = bookId;
        Update(rating, content, containsSpoilers);
        UpdatedAt = null;
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Guid BookId { get; private set; }
    public Book Book { get; private set; } = null!;
    public int Rating { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public bool ContainsSpoilers { get; private set; }
    public ICollection<ReviewComment> Comments { get; } = new List<ReviewComment>();
    public ICollection<ReviewLike> Likes { get; } = new List<ReviewLike>();

    public void Update(int rating, string content, bool containsSpoilers)
    {
        Rating = Guard.Range(rating, 1, 5, "Điểm đánh giá");
        Content = Guard.Required(content, "Nội dung đánh giá", 5000);
        ContainsSpoilers = containsSpoilers;
        Touch();
    }
}

public sealed class ReviewComment : Entity
{
    private ReviewComment() { }

    public ReviewComment(Guid reviewId, Guid userId, string content)
    {
        ReviewId = reviewId;
        UserId = userId;
        Content = Guard.Required(content, "Nội dung bình luận", 2000);
    }

    public Guid ReviewId { get; private set; }
    public Review Review { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public string Content { get; private set; } = string.Empty;
}

public sealed class ReviewLike : Entity
{
    private ReviewLike() { }
    public ReviewLike(Guid reviewId, Guid userId)
    {
        ReviewId = reviewId;
        UserId = userId;
    }

    public Guid ReviewId { get; private set; }
    public Review Review { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
}
