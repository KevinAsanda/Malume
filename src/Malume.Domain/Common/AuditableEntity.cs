namespace Malume.Domain.Common;

public abstract class AuditableEntity : Entity
{
    public DateTimeOffset CreatedAtUtc { get; protected init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAtUtc { get; protected set; }

    protected void MarkUpdated()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
