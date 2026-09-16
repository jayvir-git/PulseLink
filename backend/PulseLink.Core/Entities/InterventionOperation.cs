namespace PulseLink.Core.Entities;

// A row represents a completed operation only. It is committed in the same
// transaction as its intervention, incident version and audit event.
public class InterventionOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IncidentId { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public Guid Key { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public Guid InterventionId { get; set; }
    public DateTimeOffset PerformedAt { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
