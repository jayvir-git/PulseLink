using PulseLink.Api.Dtos;
using PulseLink.Core.Domain;
using PulseLink.Core.Entities;

namespace PulseLink.Api.Services;

public static class IncidentMapper
{
    public static IncidentSummaryDto ToSummary(Incident incident) =>
        new(
            incident.Id,
            incident.IncidentNumber,
            incident.Status,
            incident.ChiefComplaint,
            incident.Agency.Name,
            incident.DestinationHospital?.Name,
            incident.CreatedAt,
            incident.UpdatedAt);

    public static IncidentDetailDto ToDetail(Incident incident) =>
        new(
            incident.Id,
            incident.Version,
            incident.IncidentNumber,
            incident.Status,
            incident.ChiefComplaint,
            incident.PatientAgeRange,
            incident.PatientSex,
            incident.Notes,
            incident.AgencyId,
            incident.Agency.Name,
            incident.DestinationHospitalId,
            incident.DestinationHospital?.Name,
            incident.CreatedByUserId,
            incident.CreatedAt,
            incident.UpdatedAt,
            incident.HandedOffAt,
            IncidentStatusMachine.NextStatuses(incident.Status),
            incident.VitalSigns
                .OrderByDescending(v => v.RecordedAt)
                .Select(v => new VitalDto(
                    v.Id, v.RecordedAt, v.HeartRate, v.SystolicBp, v.DiastolicBp,
                    v.RespiratoryRate, v.SpO2, v.TemperatureC, v.GlasgowComaScale))
                .ToList(),
            incident.Interventions
                .OrderByDescending(i => i.PerformedAt)
                .Select(i => new InterventionDto(
                    i.Id, i.PerformedAt, i.Name, i.Medication, i.Dose, i.Route, i.Notes))
                .ToList(),
            incident.AuditEvents
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new AuditEventDto(a.Id, a.ActorUserId, a.Action, a.Details, a.CreatedAt))
                .ToList());
}
