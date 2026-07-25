namespace PulseLink.Core.Entities;

public class VitalSign
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public Incident Incident { get; set; } = null!;

    public DateTimeOffset RecordedAt { get; set; }
    public int? HeartRate { get; set; }
    public int? SystolicBp { get; set; }
    public int? DiastolicBp { get; set; }
    public int? RespiratoryRate { get; set; }
    public decimal? SpO2 { get; set; }
    public decimal? TemperatureC { get; set; }
    public string? GlasgowComaScale { get; set; }
}
