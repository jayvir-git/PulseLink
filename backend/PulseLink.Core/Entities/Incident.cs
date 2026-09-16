using PulseLink.Core.Enums;

namespace PulseLink.Core.Entities;

public class Incident
{
    public Guid Id { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public string IncidentNumber { get; set; } = string.Empty;
    public IncidentStatus Status { get; set; } = IncidentStatus.Draft;

    public Guid AgencyId { get; set; }
    public Agency Agency { get; set; } = null!;

    public Guid? DestinationHospitalId { get; set; }
    public Hospital? DestinationHospital { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;
    public string? PatientAgeRange { get; set; }
    public string? PatientSex { get; set; }
    public string ChiefComplaint { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTimeOffset? HandedOffAt { get; set; }

    public ICollection<VitalSign> VitalSigns { get; set; } = [];
    public ICollection<Intervention> Interventions { get; set; } = [];
    public ICollection<AuditEvent> AuditEvents { get; set; } = [];
}
