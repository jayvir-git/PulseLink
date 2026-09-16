using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
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

        var validationError = await ValidateDestinationAsync(IncidentStatus.Draft, request.DestinationHospitalId);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var now = DateTimeOffset.UtcNow;
        var incidentId = Guid.NewGuid();
        var incident = new Incident
        {
            Id = incidentId,
            // Reuse the full identity rather than a small daily random range.
            // This fits the existing 40-character column; old numbers stay valid.
            IncidentNumber = $"PCR-{incidentId:N}",
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

        if (CheckVersion(incident) is { } preconditionError)
        {
            return preconditionError;
        }

        if (incident.Status == IncidentStatus.HandedOff)
        {
            return BadRequest(new { message = "Handed-off incidents cannot be edited." });
        }

        var validationError = await ValidateDestinationAsync(incident.Status, request.DestinationHospitalId);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
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

        if (await SaveIncidentAsync(incident) is { } saveError)
        {
            return saveError;
        }
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

        if (CheckVersion(incident) is { } preconditionError)
        {
            return preconditionError;
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

        if (await SaveIncidentAsync(incident) is { } saveError)
        {
            return saveError;
        }
        return Ok(IncidentMapper.ToDetail((await LoadDetailAsync(id))!));
    }

    [HttpPost("{id:guid}/interventions")]
    [Authorize(Roles = AppRoles.Paramedic + "," + AppRoles.Admin)]
    public async Task<ActionResult<InterventionOperationDto>> AddIntervention(Guid id, [FromBody] AddInterventionRequest request)
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

        if (!Guid.TryParseExact(Request.Headers["Idempotency-Key"].ToString(), "D", out var key) || key == Guid.Empty)
        {
            return BadRequest(new { code = "idempotency_key_required", message = "Send a nonempty UUID in Idempotency-Key for this intervention." });
        }

        var fingerprint = InterventionRequestIdentity.Fingerprint(request);
        if (await ReplayInterventionAsync(id, key, fingerprint) is { } replay)
        {
            return replay;
        }

        if (CheckVersion(incident) is { } preconditionError)
        {
            return preconditionError;
        }

        if (incident.Status == IncidentStatus.HandedOff)
        {
            return BadRequest(new { message = "Cannot add interventions after handoff." });
        }

        var intervention = new Intervention
        {
            Id = Guid.NewGuid(),
            IncidentId = id,
            PerformedAt = DateTimeOffset.UtcNow,
            Name = request.Name.Trim(),
            Medication = request.Medication,
            Dose = request.Dose,
            Route = request.Route,
            Notes = request.Notes
        };
        db.Interventions.Add(intervention);

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

        var completed = DateTime.UtcNow;
        var operation = new InterventionOperation
        {
            IncidentId = id, ActorUserId = CurrentUserId(), Key = key, Fingerprint = fingerprint,
            InterventionId = intervention.Id, PerformedAt = intervention.PerformedAt,
            CompletedAtUtc = completed, ExpiresAtUtc = completed.AddHours(24)
        };
        db.InterventionOperations.Add(operation);
        try
        {
            if (await SaveIncidentAsync(incident) is { } saveError)
            {
                // A same-key winner may have committed while this request waited.
                // Busy failures remain retryable with the identical request key.
                if (saveError.StatusCode == StatusCodes.Status412PreconditionFailed
                    && await ReplayInterventionAsync(id, key, fingerprint) is { } winningResult)
                    return winningResult;
                return saveError;
            }
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
            if (await ReplayInterventionAsync(id, key, fingerprint) is { } winningResult)
                return winningResult;
            throw; // A different constraint failed; do not misreport it as a replay.
        }
        return Ok(new InterventionOperationDto(id, intervention.Id, intervention.PerformedAt));
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

        if (CheckVersion(incident) is { } preconditionError)
        {
            return preconditionError;
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

        if (await SaveIncidentAsync(incident) is { } saveError)
        {
            return saveError;
        }
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

    // Call only after authorizing this actor's access to the incident. The fixed
    // operation table plus actor/incident/key unique index define the key's scope.
    private async Task<ObjectResult?> ReplayInterventionAsync(Guid incidentId, Guid key, string fingerprint)
    {
        var actor = CurrentUserId();
        var operation = await db.InterventionOperations.AsNoTracking().SingleOrDefaultAsync(
            o => o.IncidentId == incidentId && o.ActorUserId == actor && o.Key == key);
        if (operation is null) return null;
        if (operation.ExpiresAtUtc <= DateTime.UtcNow)
            return StatusCode(StatusCodes.Status410Gone, new
            {
                code = "idempotency_key_expired", message = "This completed request has expired. Review the incident; do not resend it as a new intervention."
            });
        if (!string.Equals(operation.Fingerprint, fingerprint, StringComparison.Ordinal))
            return Conflict(new
            {
                code = "idempotency_key_reused", message = "This request key was already used with different intervention details."
            });
        return Ok(new InterventionOperationDto(operation.IncidentId, operation.InterventionId, operation.PerformedAt));
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 or 1555 }
            or SqlException { Number: 2601 or 2627 };

    private ObjectResult? CheckVersion(Incident incident)
    {
        var raw = Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return StatusCode(StatusCodes.Status428PreconditionRequired, new
            {
                code = "version_required", message = "Reload the incident and send its version in If-Match."
            });
        }

        // Exactly one strong, quoted GUID. Wildcards, weak tags and lists cannot
        // establish which incident state the caller reviewed.
        if (raw.Length != 38 || raw[0] != '"' || raw[^1] != '"'
            || !Guid.TryParseExact(raw[1..^1], "D", out var version))
        {
            return BadRequest(new { code = "invalid_version", message = "If-Match must contain one quoted incident version." });
        }

        return version == incident.Version ? null : VersionConflict();
    }

    private ObjectResult VersionConflict() => StatusCode(StatusCodes.Status412PreconditionFailed, new
    {
        code = "incident_conflict",
        message = "This incident changed since you loaded it. Reload and review the latest state before submitting again."
    });

    private async Task<ObjectResult?> SaveIncidentAsync(Incident incident)
    {
        // Even a no-op edit or a child append within the clock's resolution must
        // update the parent and check its original version in the database.
        db.Entry(incident).Property(i => i.Version).IsModified = true;
        try
        {
            // EF's transaction includes the incident update, child inserts and audit.
            await db.SaveChangesAsync();
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return VersionConflict();
        }
        catch (Exception ex) when (IsWriteContention(ex))
        {
            db.ChangeTracker.Clear();
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                code = "incident_busy",
                message = "The incident could not be saved because the database is busy. Reload and review before trying again."
            });
        }
    }

    private static bool IsWriteContention(Exception ex) =>
        ex is SqliteException { SqliteErrorCode: 5 or 6 } or SqlException { Number: 1205 }
        || ex is DbUpdateException { InnerException: { } inner } && IsWriteContention(inner);

    private async Task<string?> ValidateDestinationAsync(IncidentStatus status, Guid? hospitalId)
    {
        try
        {
            IncidentStatusMachine.EnsureValidState(status, hospitalId);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }

        if (hospitalId is not null && !await db.Hospitals.AnyAsync(h => h.Id == hospitalId))
        {
            return "Destination hospital does not exist.";
        }

        return null;
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
            return query.Where(IncidentRoleQueries.HospitalVisibility(CurrentHospitalId()));
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
            return IncidentRoleQueries.HospitalVisibility(CurrentHospitalId()).Compile()(incident);
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
