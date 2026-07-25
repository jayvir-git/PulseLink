using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PulseLink.Api.Dtos;
using PulseLink.Infrastructure.Data;

namespace PulseLink.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class LookupController(PulseLinkDbContext db) : ControllerBase
{
    [HttpGet("hospitals")]
    public async Task<ActionResult<IEnumerable<HospitalDto>>> Hospitals()
    {
        var items = await db.Hospitals
            .OrderBy(h => h.Name)
            .Select(h => new HospitalDto(h.Id, h.Name, h.City))
            .ToListAsync();
        return Ok(items);
    }

    [HttpGet("agencies")]
    public async Task<ActionResult<IEnumerable<AgencyDto>>> Agencies()
    {
        var items = await db.Agencies
            .OrderBy(a => a.Name)
            .Select(a => new AgencyDto(a.Id, a.Name, a.Region))
            .ToListAsync();
        return Ok(items);
    }
}
