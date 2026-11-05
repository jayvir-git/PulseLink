using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PulseLink.Core.Enums;
using PulseLink.Tests.Infrastructure;

namespace PulseLink.Tests.Api;

public class IncidentNarrativeHttpTests
{
    [Fact]
    public Task Sqlite_NarrativeBoundary() => CheckBoundary(false);

    [LocalDbFact]
    public Task SqlServer_NarrativeBoundary() => CheckBoundary(true);

    private static async Task CheckBoundary(bool sqlServer)
    {
        await using var factory = new IncidentApiFactory(sqlServer);
        using var client = factory.CreateClientAs("paramedic-a");
        var id = await factory.SeedIncidentAsync(IncidentStatus.OnScene, factory.HospitalA);
        var atLimit = new string('n', 2000);
        object Body(string notes) => new { chiefComplaint = "Narrative boundary", notes, destinationHospitalId = factory.HospitalA };
        using var accepted = await client.PutIncidentAsync($"/api/incidents/{id}", Body(atLimit));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var before = await client.GetStringAsync($"/api/incidents/{id}");
        using var rejected = await client.PutIncidentAsync($"/api/incidents/{id}", Body(atLimit + "n"));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var error = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(error.GetProperty("errors").TryGetProperty("Notes", out _));
        Assert.Equal(before, await client.GetStringAsync($"/api/incidents/{id}"));

        await using var db = factory.CreateDatabaseContext();
        var count = await db.Incidents.CountAsync();
        var audits = await db.AuditEvents.CountAsync();
        using var invalidCreate = await client.PostIncidentAsync("/api/incidents", Body(atLimit + "n"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidCreate.StatusCode);
        Assert.Equal(count, await db.Incidents.CountAsync());
        Assert.Equal(audits, await db.AuditEvents.CountAsync());
        using var validCreate = await client.PostIncidentAsync("/api/incidents", Body(atLimit));
        Assert.Equal(HttpStatusCode.Created, validCreate.StatusCode);
        var created = await validCreate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(atLimit, created.GetProperty("notes").GetString());
    }
}
