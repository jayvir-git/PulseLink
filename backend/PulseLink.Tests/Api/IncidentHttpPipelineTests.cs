using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PulseLink.Core.Enums;
using PulseLink.Infrastructure.Data;
using PulseLink.Tests.Infrastructure;

namespace PulseLink.Tests.Api;

public class IncidentHttpPipelineTests
{
    [Fact]
    public async Task Create_SucceedsWhenLegacyDailyNumberRangeIsOccupied()
    {
        await using var factory = new IncidentApiFactory();
        var now = DateTimeOffset.UtcNow;
        await using (var db = factory.CreateDatabaseContext())
        {
            // Exhaust the old Random.Next(1000, 9999) range deterministically,
            // instead of relying on a probabilistic birthday collision.
            db.Incidents.AddRange(Enumerable.Range(1000, 8999).Select(number =>
                IncidentControllerFactory.Incident(factory.AgencyA, "paramedic-a", now,
                    $"PCR-{now:yyyyMMdd}-{number}", IncidentStatus.Draft, null)));
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClientAs("paramedic-a");
        using var response = await client.PostAsJsonAsync("/api/incidents", new { chiefComplaint = "Synthetic creation check" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var verify = factory.CreateDatabaseContext();
        Assert.Equal(9000, await verify.Incidents.CountAsync());
        Assert.Equal(1, await verify.AuditEvents.CountAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("/00000000-0000-0000-0000-000000000001")]
    [InlineData("/00000000-0000-0000-0000-000000000001/export")]
    public async Task Anonymous_Read_IsUnauthorized(string suffix)
    {
        await using var factory = new IncidentApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/incidents{suffix}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("hospital-a", HttpStatusCode.Forbidden)]
    public async Task UnauthorizedActor_CannotWrite(string? actor, HttpStatusCode expected)
    {
        await using var factory = new IncidentApiFactory();
        var id = await factory.SeedIncidentAsync(IncidentStatus.Transporting, factory.HospitalA);
        using var client = actor is null ? factory.CreateClient() : factory.CreateClientAs(actor);
        var edit = new
        {
            chiefComplaint = "Changed test",
            destinationHospitalId = factory.HospitalA
        };
        using var create = await client.PostAsJsonAsync("/api/incidents", edit);
        using var update = await client.PutAsJsonAsync($"/api/incidents/{id}", edit);
        using var vital = await client.PostAsJsonAsync($"/api/incidents/{id}/vitals", new { heartRate = 80 });
        using var intervention = await client.PostAsJsonAsync($"/api/incidents/{id}/interventions", new { name = "Test intervention" });
        using var status = await client.PostAsJsonAsync($"/api/incidents/{id}/status", new { toStatus = "Arrived" });

        foreach (var response in new[] { create, update, vital, intervention, status })
            Assert.Equal(expected, response.StatusCode);
        await using var db = factory.CreateDatabaseContext();
        var incident = Assert.Single(await db.Incidents.ToListAsync());
        Assert.Equal("Test", incident.ChiefComplaint);
        Assert.Equal(IncidentStatus.Transporting, incident.Status);
        Assert.Equal(0, await db.VitalSigns.CountAsync());
        Assert.Equal(0, await db.Interventions.CountAsync());
        Assert.Equal(0, await db.AuditEvents.CountAsync());
    }

    [Theory]
    [InlineData("paramedic-a")]
    [InlineData("admin")]
    public async Task AuthorizedWriter_CanCreateEditAppendAndTransition(string actor)
    {
        await using var factory = new IncidentApiFactory();
        using var client = factory.CreateClientAs(actor);
        using var created = await client.PostIncidentAsync("/api/incidents", new
        {
            chiefComplaint = "Synthetic test", destinationHospitalId = factory.HospitalA
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var content = await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var id = content.GetProperty("id").GetGuid();

        using var updated = await client.PutIncidentAsync($"/api/incidents/{id}", new
        {
            chiefComplaint = "Updated test", destinationHospitalId = factory.HospitalA
        });
        using var vital = await client.PostIncidentAsync($"/api/incidents/{id}/vitals", new { heartRate = 80 });
        using var intervention = await client.PostIncidentAsync($"/api/incidents/{id}/interventions", new { name = "Test intervention" });
        using var status = await client.PostIncidentAsync($"/api/incidents/{id}/status", new { toStatus = "EnRoute" });
        foreach (var response in new[] { updated, vital, intervention, status })
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var db = factory.CreateDatabaseContext();
        var incident = Assert.Single(await db.Incidents.ToListAsync());
        Assert.Equal("Updated test", incident.ChiefComplaint);
        Assert.Equal(IncidentStatus.EnRoute, incident.Status);
        Assert.Equal(1, await db.VitalSigns.CountAsync());
        Assert.Equal(1, await db.Interventions.CountAsync());
        Assert.Equal(5, await db.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task Factories_IsolateRequestData_AndStartupSeeding()
    {
        await using var first = new IncidentApiFactory();
        await using var second = new IncidentApiFactory();
        var id = await first.SeedIncidentAsync(IncidentStatus.Draft, first.HospitalA);
        using var firstClient = first.CreateClientAs("admin");
        using var secondClient = second.CreateClientAs("admin");

        using var found = await firstClient.GetAsync($"/api/incidents/{id}");
        using var missing = await secondClient.GetAsync($"/api/incidents/{id}");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var scope = first.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PulseLinkDbContext>();
        if (LocalDb.Required)
        {
            Assert.IsType<SqlServerPulseLinkDbContext>(db);
            Assert.StartsWith("PulseLink_ConcurrencyTest_", db.Database.GetDbConnection().Database);
        }
        else
        {
            Assert.IsType<SqlitePulseLinkDbContext>(db);
            Assert.Contains("Mode=Memory", db.Database.GetConnectionString());
        }
        Assert.Equal(2, await db.Agencies.CountAsync());
        Assert.Equal(2, await db.Hospitals.CountAsync());
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(AppRoles.All.Length, await db.Roles.CountAsync());
    }
}
