using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PulseLink.Core.Enums;

namespace PulseLink.Tests.Api;

public class IncidentVisibilityHttpTests
{
    [Theory]
    [InlineData(IncidentStatus.Draft, "")]
    [InlineData(IncidentStatus.Draft, "/export")]
    [InlineData(IncidentStatus.EnRoute, "")]
    [InlineData(IncidentStatus.EnRoute, "/export")]
    [InlineData(IncidentStatus.OnScene, "")]
    [InlineData(IncidentStatus.OnScene, "/export")]
    public async Task Hospital_EarlyIncident_IsForbidden(IncidentStatus status, string suffix)
    {
        await using var factory = new IncidentApiFactory();
        var id = await factory.SeedIncidentAsync(status, factory.HospitalA);
        using var client = factory.CreateClientAs("hospital-a");

        using var response = await client.GetAsync($"/api/incidents/{id}{suffix}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var db = factory.CreateDatabaseContext();
        Assert.Equal(0, await db.AuditEvents.CountAsync());
    }

    [Theory]
    [InlineData("hospital-a")]
    [InlineData("hospital-b")]
    public async Task Hospital_ListDetailAndExport_HaveSameVisibility(string actor)
    {
        await using var factory = new IncidentApiFactory();
        using var client = factory.CreateClientAs(actor);
        var ownHospital = actor == "hospital-a" ? factory.HospitalA : factory.HospitalB;
        var otherHospital = actor == "hospital-a" ? factory.HospitalB : factory.HospitalA;
        var visible = new List<Guid>();
        var hidden = new List<Guid>();
        foreach (var status in Enum.GetValues<IncidentStatus>())
        {
            var ownId = await factory.SeedIncidentAsync(status, ownHospital);
            (status >= IncidentStatus.Transporting ? visible : hidden).Add(ownId);
            hidden.Add(await factory.SeedIncidentAsync(status, otherHospital));
        }

        await AssertListAsync(client, visible);
        foreach (var id in visible) await AssertReadAndExportAsync(client, id, HttpStatusCode.OK);
        foreach (var id in hidden) await AssertReadAndExportAsync(client, id, HttpStatusCode.Forbidden);

        await using var db = factory.CreateDatabaseContext();
        var exports = await db.AuditEvents.Where(a => a.Action == "HandoffExported").ToListAsync();
        Assert.Equal(visible.Count, exports.Count);
        Assert.All(exports, audit =>
        {
            Assert.Contains(audit.IncidentId!.Value, visible);
            Assert.Equal(actor, audit.ActorUserId);
        });
    }

    [Theory]
    [InlineData("hospital-missing")]
    [InlineData("hospital-invalid")]
    public async Task Hospital_WithoutValidAffiliation_CannotSeeUndestinedIncidents(string actor)
    {
        await using var factory = new IncidentApiFactory();
        using var client = factory.CreateClientAs(actor);
        var ids = new List<Guid>();
        foreach (var status in Enum.GetValues<IncidentStatus>())
            ids.Add(await factory.SeedIncidentAsync(status, hospitalId: null));
        ids.Add(await factory.SeedIncidentAsync(IncidentStatus.Transporting, factory.HospitalA));

        await AssertListAsync(client, []);
        foreach (var id in ids) await AssertReadAndExportAsync(client, id, HttpStatusCode.Forbidden);
        await using var db = factory.CreateDatabaseContext();
        Assert.Equal(0, await db.AuditEvents.CountAsync());
    }

    [Theory]
    [InlineData("paramedic-a")]
    [InlineData("paramedic-b")]
    public async Task Paramedic_AgencyOrCreatorAccess_IsPreserved(string actor)
    {
        await using var factory = new IncidentApiFactory();
        using var client = factory.CreateClientAs(actor);
        var ownAgency = actor == "paramedic-a" ? factory.AgencyA : factory.AgencyB;
        var otherAgency = actor == "paramedic-a" ? factory.AgencyB : factory.AgencyA;
        var own = await factory.SeedIncidentAsync(IncidentStatus.Draft, null, ownAgency, actor);
        var colleague = await factory.SeedIncidentAsync(IncidentStatus.OnScene, null, ownAgency, "colleague");
        var createdElsewhere = await factory.SeedIncidentAsync(IncidentStatus.EnRoute, null, otherAgency, actor);
        var hidden = await factory.SeedIncidentAsync(IncidentStatus.Transporting, factory.HospitalB, otherAgency, "other");

        Guid[] visible = [own, colleague, createdElsewhere];
        await AssertListAsync(client, visible);
        foreach (var id in visible) await AssertReadAndExportAsync(client, id, HttpStatusCode.OK);
        await AssertReadAndExportAsync(client, hidden, HttpStatusCode.Forbidden);
        using var deniedEdit = await client.PutAsJsonAsync($"/api/incidents/{hidden}", new { chiefComplaint = "Denied edit" });
        Assert.Equal(HttpStatusCode.Forbidden, deniedEdit.StatusCode);
        await using var db = factory.CreateDatabaseContext();
        Assert.Equal(0, await db.AuditEvents.CountAsync(a => a.IncidentId == hidden));
        Assert.Equal("Test", (await db.Incidents.FindAsync(hidden))!.ChiefComplaint);
    }

    [Fact]
    public async Task Admin_CanReadAndExportAcrossAgenciesHospitalsAndStatuses()
    {
        await using var factory = new IncidentApiFactory();
        using var client = factory.CreateClientAs("admin");
        var ids = new List<Guid>();
        foreach (var status in Enum.GetValues<IncidentStatus>())
        {
            ids.Add(await factory.SeedIncidentAsync(status, factory.HospitalA, factory.AgencyA));
            ids.Add(await factory.SeedIncidentAsync(status, factory.HospitalB, factory.AgencyB));
        }
        await AssertListAsync(client, ids);
        foreach (var id in ids) await AssertReadAndExportAsync(client, id, HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("hospital-a")]
    [InlineData("paramedic-a")]
    [InlineData("admin")]
    public async Task MissingIncident_RemainsNotFound(string actor)
    {
        await using var factory = new IncidentApiFactory();
        using var client = factory.CreateClientAs(actor);
        await AssertReadAndExportAsync(client, Guid.NewGuid(), HttpStatusCode.NotFound);
    }

    private static async Task AssertListAsync(HttpClient client, IEnumerable<Guid> expected)
    {
        using var response = await client.GetAsync("/api/incidents?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(expected.OrderBy(id => id), ids.OrderBy(id => id));
        Assert.Equal(ids.Length, page.GetProperty("totalCount").GetInt32());
    }

    private static async Task AssertReadAndExportAsync(HttpClient client, Guid id, HttpStatusCode expected)
    {
        using var detail = await client.GetAsync($"/api/incidents/{id}");
        using var export = await client.GetAsync($"/api/incidents/{id}/export");
        Assert.Equal(expected, detail.StatusCode);
        Assert.Equal(expected, export.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var record = await detail.Content.ReadFromJsonAsync<JsonElement>();
            var bundle = await export.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(id, record.GetProperty("id").GetGuid());
            Assert.Equal("Bundle", bundle.GetProperty("resourceType").GetString());
            Assert.Equal(id, bundle.GetProperty("entry")[0].GetProperty("resource").GetProperty("id").GetGuid());
        }
    }
}
