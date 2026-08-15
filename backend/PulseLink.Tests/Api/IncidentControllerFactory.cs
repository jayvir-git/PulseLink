using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PulseLink.Api.Controllers;
using PulseLink.Api.Dtos;
using PulseLink.Core.Entities;
using PulseLink.Core.Enums;
using PulseLink.Infrastructure.Data;

namespace PulseLink.Tests.Api;

internal static class IncidentControllerFactory
{
    public static IncidentsController Create(
        PulseLinkDbContext db,
        string role,
        string userId,
        Guid? agencyId = null,
        Guid? hospitalId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role),
            new("agencyId", agencyId?.ToString() ?? string.Empty),
            new("hospitalId", hospitalId?.ToString() ?? string.Empty)
        };

        return new IncidentsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            }
        };
    }

    public static async Task<PagedIncidentListDto> ListAsync(
        IncidentsController controller,
        int page = 1,
        int pageSize = 50)
    {
        var result = await controller.List(page, pageSize);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<PagedIncidentListDto>(ok.Value);
    }

    public static async Task<IncidentDetailDto> GetAsync(IncidentsController controller, Guid id)
    {
        var result = await controller.Get(id);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<IncidentDetailDto>(ok.Value);
    }

    public static Incident Incident(
        Guid agencyId,
        string createdBy,
        DateTimeOffset updated,
        string number,
        IncidentStatus status = IncidentStatus.Draft,
        Guid? hospitalId = null,
        Guid? id = null)
    {
        return new Incident
        {
            Id = id ?? Guid.NewGuid(),
            IncidentNumber = number,
            Status = status,
            AgencyId = agencyId,
            DestinationHospitalId = hospitalId,
            CreatedByUserId = createdBy,
            ChiefComplaint = "Test",
            CreatedAt = updated.AddHours(-1),
            UpdatedAt = updated,
            UpdatedAtUtc = updated.UtcDateTime
        };
    }
}
