using System.ComponentModel.DataAnnotations;
using PulseLink.Core.Enums;

namespace PulseLink.Api.Dtos;

public record CreateIncidentRequest(
    [Required, MaxLength(500)] string ChiefComplaint,
    string? PatientAgeRange,
    string? PatientSex,
    string? Notes,
    Guid? DestinationHospitalId);

public record UpdateIncidentRequest(
    [Required, MaxLength(500)] string ChiefComplaint,
    string? PatientAgeRange,
    string? PatientSex,
    string? Notes,
    Guid? DestinationHospitalId);

public record AddVitalRequest(
    int? HeartRate,
    int? SystolicBp,
    int? DiastolicBp,
    int? RespiratoryRate,
    decimal? SpO2,
    decimal? TemperatureC,
    string? GlasgowComaScale);

public record AddInterventionRequest(
    [Required, MaxLength(200)] string Name,
    string? Medication,
    string? Dose,
    string? Route,
    string? Notes);

public record TransitionStatusRequest([Required] IncidentStatus ToStatus);

public record VitalDto(
    Guid Id,
    DateTimeOffset RecordedAt,
    int? HeartRate,
    int? SystolicBp,
    int? DiastolicBp,
    int? RespiratoryRate,
    decimal? SpO2,
    decimal? TemperatureC,
    string? GlasgowComaScale);

public record InterventionDto(
    Guid Id,
    DateTimeOffset PerformedAt,
    string Name,
    string? Medication,
    string? Dose,
    string? Route,
    string? Notes);

public record AuditEventDto(
    Guid Id,
    string ActorUserId,
    string Action,
    string Details,
    DateTimeOffset CreatedAt);

public record IncidentSummaryDto(
    Guid Id,
    string IncidentNumber,
    IncidentStatus Status,
    string ChiefComplaint,
    string AgencyName,
    string? DestinationHospitalName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record IncidentDetailDto(
    Guid Id,
    string IncidentNumber,
    IncidentStatus Status,
    string ChiefComplaint,
    string? PatientAgeRange,
    string? PatientSex,
    string? Notes,
    Guid AgencyId,
    string AgencyName,
    Guid? DestinationHospitalId,
    string? DestinationHospitalName,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? HandedOffAt,
    IReadOnlyList<IncidentStatus> AllowedNextStatuses,
    IReadOnlyList<VitalDto> VitalSigns,
    IReadOnlyList<InterventionDto> Interventions,
    IReadOnlyList<AuditEventDto> AuditEvents);

public record HospitalDto(Guid Id, string Name, string City);
public record AgencyDto(Guid Id, string Name, string Region);
