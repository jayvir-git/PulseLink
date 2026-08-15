using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PulseLink.Api.Dtos;
using PulseLink.Api.Services;
using PulseLink.Core.Domain;
using PulseLink.Core.Entities;
using PulseLink.Core.Enums;
using PulseLink.Infrastructure.Data;

namespace PulseLink.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class IncidentsController(PulseLinkDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedIncidentListDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = IncidentListQuery.DefaultPageSize)
    {
        (page, pageSize) = IncidentListQuery.Normalize(page, pageSize);

        var filtered = ApplyRoleFilter(db.Incidents.AsNoTracking());
        var totalCount = await filtered.CountAsync();

        // Order by UpdatedAtUtc (DateTime) so SQLite and SQL Server both sort in SQL.
        // DateTimeOffset cannot be ORDER BY'd in SQLite; this column exists for that reason.
        var items = await IncidentListQuery
            .ApplyOrderAndPage(
                filtered.Include(i => i.Agency).Include(i => i.DestinationHospital),
                page,
                pageSize)
            .ToListAsync();

        return Ok(new PagedIncidentListDto(
            items.Select(IncidentMapper.ToSummary).ToList(),
            page,
            pageSize,
            totalCount));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<IncidentDetailDto>> Get(Guid id)
    {
        var incident = await LoadDetailAsync(id);
        if (incident is null)
        {
            return NotFound();
        }

        if (!CanAccess(incident))
        {
            return Forbid();
        }

        return Ok(IncidentMapper.ToDetail(incident));
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Paramedic + "," + AppRoles.Admin)]
    public async Task<ActionResult<IncidentDetailDto>> Create([FromBody] CreateIncidentRequest request)
    {
        var userId = CurrentUserId();
        var agencyId = CurrentAgencyId();
        if (agencyId is null && !User.IsInRole(AppRoles.Admin))
        {
            return BadRequest(new { message = "Paramedic account is not linked to an agency." });
        }

        if (agencyId is null)
        {
            agencyId = await db.Agencies.Select(a => (Guid?)a.Id).FirstOrDefaultAsync();
        }

        if (agencyId is null)
        {
            return BadRequest(new { message = "No agency is configured." });
        }

        var now = DateTimeOffset.UtcNow;
        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            IncidentNumber = $"PCR-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            Status = IncidentStatus.Draft,
            AgencyId = agencyId.Value,
            DestinationHospitalId = request.DestinationHospitalId,
            CreatedByUserId = userId,
            ChiefComplaint = request.ChiefComplaint.Trim(),
            PatientAgeRange = request.PatientAgeRange,
            PatientSex = request.PatientSex,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Incidents.Add(incident);
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            ActorUserId = userId,
            Action = "IncidentCreated",
            Details = $"Created draft incident {incident.IncidentNumber}",
            CreatedAt = now
        });

        await db.SaveChangesAsync();
        var created = await LoadDetailAsync(incident.Id);
        return CreatedAtAction(nameof(Get), new { id = incident.Id }, IncidentMapper.ToDetail(created!));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = AppRoles.Paramedic + "," + AppRoles.Admin)]
    public async Task<ActionResult<IncidentDetailDto>> Update(Guid id, [FromBody] UpdateIncidentRequest request)
    {
        var incident = await db.Incidents.FirstOrDefaultAsync(i => i.Id == id);
        if (incident is null)
        {
            return NotFound();
        }

        if (!CanAccess(incident))
        {
            return Forbid();
        }

        if (incident.Status == IncidentStatus.HandedOff)
        {
            return BadRequest(new { message = "Handed-off incidents cannot be edited." });
        }

        incident.ChiefComplaint = request.ChiefComplaint.Trim();
        incident.PatientAgeRange = request.PatientAgeRange;
        incident.PatientSex = request.PatientSex;
        incident.Notes = request.Notes;
        incident.DestinationHospitalId = request.DestinationHospitalId;
        incident.UpdatedAt = DateTimeOffset.UtcNow;

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            ActorUserId = CurrentUserId(),
            Action = "IncidentUpdated",
            Details = "Updated incident clinical details",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
        return Ok(IncidentMapper.ToDetail((await LoadDetailAsync(id))!));
    }

    [HttpPost("{id:guid}/vitals")]
    [Authorize(Roles = AppRoles.Paramedic + "," + AppRoles.Admin)]
    public async Task<ActionResult<IncidentDetailDto>> AddVital(Guid id, [FromBody] AddVitalRequest request)
    {
        var incident = await db.Incidents.FirstOrDefaultAsync(i => i.Id == id);
        if (incident is null)
        {
            return NotFound();
        }

        if (!CanAccess(incident))
        {
            return Forbid();
        }

        if (incident.Status == IncidentStatus.HandedOff)
        {
            return BadRequest(new { message = "Cannot add vitals after handoff." });
        }

        var vital = new VitalSign
        {
            Id = Guid.NewGuid(),
            IncidentId = id,
            RecordedAt = DateTimeOffset.UtcNow,
            HeartRate = request.HeartRate,
            SystolicBp = request.SystolicBp,
            DiastolicBp = request.DiastolicBp,
            RespiratoryRate = request.RespiratoryRate,
            SpO2 = request.SpO2,
            TemperatureC = request.TemperatureC,
            GlasgowComaScale = request.GlasgowComaScale
        };

        db.VitalSigns.Add(vital);
        incident.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            IncidentId = id,
            ActorUserId = CurrentUserId(),
            Action = "VitalAdded",
            Details = $"Recorded vitals HR={request.HeartRate} BP={request.SystolicBp}/{request.DiastolicBp}",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
        return Ok(IncidentMapper.ToDetail((await LoadDetailAsync(id))!));
    }

    [HttpPost("{id:guid}/interventions")]
    [Authorize(Roles = AppRoles.Paramedic + "," + AppRoles.Admin)]
    public async Task<ActionResult<IncidentDetailDto>> AddIntervention(Guid id, [FromBody] AddInterventionRequest request)
    {
        var incident = await db.Incidents.FirstOrDefaultAsync(i => i.Id == id);
        if (incident is null)
        {
            return NotFound();
        }

        if (!CanAccess(incident))
        {
            return Forbid();
        }

        if (incident.Status == IncidentStatus.HandedOff)
        {
            return BadRequest(new { message = "Cannot add interventions after handoff." });
        }

        db.Interventions.Add(new Intervention
        {
            Id = Guid.NewGuid(),
            IncidentId = id,
            PerformedAt = DateTimeOffset.UtcNow,
            Name = request.Name.Trim(),
            Medication = request.Medication,
            Dose = request.Dose,
            Route = request.Route,
            Notes = request.Notes
        });

        incident.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            IncidentId = id,
            ActorUserId = CurrentUserId(),
            Action = "InterventionAdded",
            Details = $"Added intervention: {request.Name}",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
        return Ok(IncidentMapper.ToDetail((await LoadDetailAsync(id))!));
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Roles = AppRoles.Paramedic + "," + AppRoles.Admin)]
    public async Task<ActionResult<IncidentDetailDto>> Transition(Guid id, [FromBody] TransitionStatusRequest request)
    {
        var incident = await db.Incidents.FirstOrDefaultAsync(i => i.Id == id);
        if (incident is null)
        {
            return NotFound();
        }

        if (!CanAccess(incident))
        {
            return Forbid();
        }

        try
        {
            IncidentStatusMachine.EnsureCanTransition(incident.Status, request.ToStatus, incident.DestinationHospitalId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var from = incident.Status;
        incident.Status = request.ToStatus;
        incident.UpdatedAt = DateTimeOffset.UtcNow;
        if (request.ToStatus == IncidentStatus.HandedOff)
        {
            incident.HandedOffAt = DateTimeOffset.UtcNow;
        }

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            IncidentId = id,
            ActorUserId = CurrentUserId(),
            Action = "StatusChanged",
            Details = $"Status changed from {from} to {request.ToStatus}",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
        return Ok(IncidentMapper.ToDetail((await LoadDetailAsync(id))!));
    }

    [HttpGet("{id:guid}/export")]
    [Authorize(Roles = AppRoles.HospitalStaff + "," + AppRoles.Admin + "," + AppRoles.Paramedic)]
    public async Task<IActionResult> Export(Guid id)
    {
        var incident = await LoadDetailAsync(id);
        if (incident is null)
        {
            return NotFound();
        }

        if (!CanAccess(incident))
        {
            return Forbid();
        }

        var payload = new
        {
            resourceType = "Bundle",
            type = "document",
            timestamp = DateTimeOffset.UtcNow,
            entry = new object[]
            {
                new
                {
                    resource = new
                    {
                        resourceType = "Encounter",
                        id = incident.Id,
                        status = incident.Status.ToString(),
                        identifier = new[] { new { system = "urn:pulselink:incident", value = incident.IncidentNumber } },
                        period = new { start = incident.CreatedAt, end = incident.HandedOffAt },
                        serviceProvider = new { display = incident.Agency.Name },
                        hospitalization = new
                        {
                            destination = incident.DestinationHospital is null
                                ? null
                                : new { display = incident.DestinationHospital.Name }
                        }
                    }
                },
                new
                {
                    resource = new
                    {
                        resourceType = "Condition",
                        code = new { text = incident.ChiefComplaint },
                        subject = new { display = "Prehospital patient (de-identified)" }
                    }
                },
                new
                {
                    resource = new
                    {
                        resourceType = "Observation",
                        category = "vital-signs",
                        component = incident.VitalSigns.Select(v => new
                        {
                            recordedAt = v.RecordedAt,
                            heartRate = v.HeartRate,
                            bloodPressure = v.SystolicBp is null ? null : $"{v.SystolicBp}/{v.DiastolicBp}",
                            respiratoryRate = v.RespiratoryRate,
                            spo2 = v.SpO2,
                            temperatureC = v.TemperatureC,
                            gcs = v.GlasgowComaScale
                        })
                    }
                },
                new
                {
                    resource = new
                    {
                        resourceType = "Procedure",
                        item = incident.Interventions.Select(i => new
                        {
                            performedAt = i.PerformedAt,
                            name = i.Name,
                            medication = i.Medication,
                            dose = i.Dose,
                            route = i.Route
                        })
                    }
                }
            }
        };

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            IncidentId = id,
            ActorUserId = CurrentUserId(),
            Action = "HandoffExported",
            Details = "Generated hospital handoff export payload",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        return Ok(payload);
    }

    private async Task<Incident?> LoadDetailAsync(Guid id) =>
        await db.Incidents
            .AsNoTracking()
            .Include(i => i.Agency)
            .Include(i => i.DestinationHospital)
            .Include(i => i.VitalSigns)
            .Include(i => i.Interventions)
            .Include(i => i.AuditEvents)
            .FirstOrDefaultAsync(i => i.Id == id);

    private IQueryable<Incident> ApplyRoleFilter(IQueryable<Incident> query)
    {
        if (User.IsInRole(AppRoles.Admin))
        {
            return query;
        }

        if (User.IsInRole(AppRoles.HospitalStaff))
        {
            var hospitalId = CurrentHospitalId();
            return query.Where(i => i.DestinationHospitalId == hospitalId
                && (i.Status == IncidentStatus.Arrived || i.Status == IncidentStatus.HandedOff || i.Status == IncidentStatus.Transporting));
        }

        var agencyId = CurrentAgencyId();
        var userId = CurrentUserId();
        return IncidentRoleQueries.ForParamedic(query, agencyId, userId);
    }

    private bool CanAccess(Incident incident)
    {
        if (User.IsInRole(AppRoles.Admin))
        {
            return true;
        }

        if (User.IsInRole(AppRoles.HospitalStaff))
        {
            return incident.DestinationHospitalId == CurrentHospitalId();
        }

        return incident.AgencyId == CurrentAgencyId() || incident.CreatedByUserId == CurrentUserId();
    }

    private string CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Missing user id claim.");

    private Guid? CurrentAgencyId()
    {
        var raw = User.FindFirstValue("agencyId");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private Guid? CurrentHospitalId()
    {
        var raw = User.FindFirstValue("hospitalId");
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
