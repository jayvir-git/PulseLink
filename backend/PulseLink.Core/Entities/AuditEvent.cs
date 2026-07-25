namespace PulseLink.Core.Entities;

public class AuditEvent
{
    public Guid Id { get; set; }
    public Guid? IncidentId { get; set; }
    public Incident? Incident { get; set; }

    public string ActorUserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
