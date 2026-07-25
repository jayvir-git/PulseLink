namespace PulseLink.Core.Entities;

public class Intervention
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public Incident Incident { get; set; } = null!;

    public DateTimeOffset PerformedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Medication { get; set; }
    public string? Dose { get; set; }
    public string? Route { get; set; }
    public string? Notes { get; set; }
}
