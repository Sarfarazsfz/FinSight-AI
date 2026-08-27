using FinSight.Domain.Enums;

namespace FinSight.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; private set; }

    public Guid? RunId { get; private set; }

    public AuditEventType EventType { get; private set; }

    public string DetailPayload { get; private set; } = string.Empty;

    public string? RelatedEntityType { get; private set; }

    public Guid? RelatedEntityId { get; private set; }

    public DateTime OccurredAt { get; private set; }

    private AuditLog()
    {
    }

    public AuditLog(
        AuditEventType eventType,
        string detailPayload,
        Guid? runId = null,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null)
    {
        if (string.IsNullOrWhiteSpace(detailPayload))
        {
            throw new ArgumentException(
                "Audit detail payload is required.",
                nameof(detailPayload));
        }

        if (runId == Guid.Empty)
        {
            throw new ArgumentException(
                "Run ID cannot be an empty GUID.",
                nameof(runId));
        }

        if ((relatedEntityType is null) != (relatedEntityId is null))
        {
            throw new ArgumentException(
                "RelatedEntityType and RelatedEntityId must both be provided or both be null.");
        }

        if (relatedEntityId == Guid.Empty)
        {
            throw new ArgumentException(
                "Related entity ID cannot be an empty GUID.",
                nameof(relatedEntityId));
        }

        Id = Guid.NewGuid();
        RunId = runId;
        EventType = eventType;
        DetailPayload = detailPayload.Trim();

        RelatedEntityType =
            string.IsNullOrWhiteSpace(relatedEntityType)
                ? null
                : relatedEntityType.Trim();

        RelatedEntityId = relatedEntityId;

        OccurredAt = DateTime.UtcNow;
    }
}