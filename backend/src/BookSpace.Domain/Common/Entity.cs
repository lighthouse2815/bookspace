namespace BookSpace.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; protected set; }
    public DateTimeOffset? DeletedAt { get; protected set; }

    public bool IsDeleted => DeletedAt.HasValue;

    protected void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    public virtual void SoftDelete()
    {
        if (DeletedAt.HasValue)
        {
            throw new Exceptions.DomainException("RESOURCE_ALREADY_DELETED", "Dữ liệu đã được xóa trước đó.");
        }

        DeletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DeletedAt;
    }
}
