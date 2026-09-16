using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PulseLink.Core.Enums;

namespace PulseLink.Tests.Api;

public class IncidentStateHttpTests
{
    [Theory]
    [InlineData(IncidentStatus.Transporting)]
    [InlineData(IncidentStatus.Arrived)]
    public async Task Update_CannotClearRequiredDestination(IncidentStatus status)
    {
        await using var factory = new IncidentApiFactory();
        var id = await factory.SeedIncidentAsync(status, factory.HospitalA);
        using var client = factory.CreateClientAs("paramedic-a");
        var before = await SnapshotAsync(factory, id);

        using var response = await client.PutIncidentAsync($"/api/incidents/{id}", Edit(null));

        await AssertBadRequestAsync(response, "A destination hospital is required before transport or handoff.");
        Assert.Equal(before, await SnapshotAsync(factory, id));
    }

    [Theory]
    [InlineData(IncidentStatus.Draft)]
    [InlineData(IncidentStatus.Transporting)]
    [InlineData(IncidentStatus.Arrived)]
    public async Task Update_NonexistentHospital_IsControlledErrorWithoutWrites(IncidentStatus status)
    {
        await using var factory = new IncidentApiFactory();
        var id = await factory.SeedIncidentAsync(status, factory.HospitalA);
        using var client = factory.CreateClientAs("paramedic-a");
        var before = await SnapshotAsync(factory, id);

        using var response = await client.PutIncidentAsync($"/api/incidents/{id}", Edit(Guid.NewGuid()));

        await AssertBadRequestAsync(response, "Destination hospital does not exist.");
        Assert.Equal(before, await SnapshotAsync(factory, id));
    }

    [Fact]
    public async Task Create_NonexistentHospital_IsControlledErrorWithoutWrites()
    {
        await using var factory = new IncidentApiFactory();
        using var client = factory.CreateClientAs("paramedic-a");

        using var response = await client.PostIncidentAsync("/api/incidents", Edit(Guid.NewGuid()));

        await AssertBadRequestAsync(response, "Destination hospital does not exist.");
        await using var db = factory.CreateDatabaseContext();
        Assert.Equal(0, await db.Incidents.CountAsync());
        Assert.Equal(0, await db.AuditEvents.CountAsync());
    }

    [Theory]
    [InlineData(IncidentStatus.Draft)]
    [InlineData(IncidentStatus.EnRoute)]
    [InlineData(IncidentStatus.OnScene)]
    public async Task Update_BeforeTransport_CanClearDestination(IncidentStatus status)
    {
        await using var factory = new IncidentApiFactory();
        var id = await factory.SeedIncidentAsync(status, factory.HospitalA);
        using var client = factory.CreateClientAs("paramedic-a");

        using var response = await client.PutIncidentAsync($"/api/incidents/{id}", Edit(null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await SnapshotAsync(factory, id);
        Assert.Null(after.DestinationHospitalId);
        Assert.Equal(status, after.Status);
        Assert.Equal("Changed complaint", after.ChiefComplaint);
        Assert.Equal(1, after.Audits);
    }

    [Theory]
    [InlineData(IncidentStatus.Transporting)]
    [InlineData(IncidentStatus.Arrived)]
    public async Task Update_DuringTransport_CanChangeToAnotherExistingHospital(IncidentStatus status)
    {
        await using var factory = new IncidentApiFactory();
        var id = await factory.SeedIncidentAsync(status, factory.HospitalA);
        using var client = factory.CreateClientAs("paramedic-a");

        using var response = await client.PutIncidentAsync($"/api/incidents/{id}", Edit(factory.HospitalB));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await SnapshotAsync(factory, id);
        Assert.Equal(factory.HospitalB, after.DestinationHospitalId);
        Assert.Equal(status, after.Status);
        Assert.Equal("Changed complaint", after.ChiefComplaint);
        Assert.Equal(1, after.Audits);
        using var oldHospital = factory.CreateClientAs("hospital-a");
        using var newHospital = factory.CreateClientAs("hospital-b");
        using var oldDetail = await oldHospital.GetAsync($"/api/incidents/{id}");
        using var newDetail = await newHospital.GetAsync($"/api/incidents/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, oldDetail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newDetail.StatusCode);
    }

    [Theory]
    [InlineData("paramedic-a")]
    [InlineData("admin")]
    public async Task HandedOff_RejectsAllClinicalWritesWithoutSideEffects(string actor)
    {
        await using var factory = new IncidentApiFactory();
        var id = await factory.SeedIncidentAsync(IncidentStatus.HandedOff, factory.HospitalA);
        using var client = factory.CreateClientAs(actor);
        var before = await SnapshotAsync(factory, id);

        using var edit = await client.PutIncidentAsync($"/api/incidents/{id}", Edit(factory.HospitalB));
        using var vital = await client.PostIncidentAsync($"/api/incidents/{id}/vitals", new { heartRate = 80 });
        using var intervention = await client.PostIncidentAsync($"/api/incidents/{id}/interventions", new { name = "Test intervention" });
        using var transition = await client.PostIncidentAsync($"/api/incidents/{id}/status", new { toStatus = "Arrived" });

        await AssertBadRequestAsync(edit, "Handed-off incidents cannot be edited.");
        await AssertBadRequestAsync(vital, "Cannot add vitals after handoff.");
        await AssertBadRequestAsync(intervention, "Cannot add interventions after handoff.");
        Assert.Equal(HttpStatusCode.BadRequest, transition.StatusCode);
        Assert.Equal(before, await SnapshotAsync(factory, id));
    }

    [Fact]
    public async Task ForwardLifecycle_RequiresDestinationAndStillReachesHandoff()
    {
        await using var factory = new IncidentApiFactory();
        using var client = factory.CreateClientAs("paramedic-a");
        using var create = await client.PostIncidentAsync("/api/incidents", Edit(null));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var payload = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = payload.GetProperty("id").GetGuid();

        foreach (var status in new[] { "EnRoute", "OnScene" })
        {
            using var response = await client.PostIncidentAsync($"/api/incidents/{id}/status", new { toStatus = status });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        var before = await SnapshotAsync(factory, id);
        using var denied = await client.PostIncidentAsync($"/api/incidents/{id}/status", new { toStatus = "Transporting" });
        await AssertBadRequestAsync(denied, "A destination hospital is required before transport or handoff.");
        Assert.Equal(before, await SnapshotAsync(factory, id));

        using var edit = await client.PutIncidentAsync($"/api/incidents/{id}", Edit(factory.HospitalA));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        foreach (var status in new[] { "Transporting", "Arrived", "HandedOff" })
        {
            using var response = await client.PostIncidentAsync($"/api/incidents/{id}/status", new { toStatus = status });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        var after = await SnapshotAsync(factory, id);
        Assert.Equal(IncidentStatus.HandedOff, after.Status);
        Assert.NotNull(after.HandedOffAt);
        Assert.Equal(factory.HospitalA, after.DestinationHospitalId);
        Assert.Equal(7, after.Audits);
    }

    private static object Edit(Guid? hospitalId) => new
    {
        chiefComplaint = "Changed complaint", patientAgeRange = "Adult",
        patientSex = "Unknown", notes = "Synthetic updated notes", destinationHospitalId = hospitalId
    };

    private static async Task AssertBadRequestAsync(HttpResponseMessage response, string message)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(message, body.GetProperty("message").GetString());
    }

    private static async Task<StateSnapshot> SnapshotAsync(IncidentApiFactory factory, Guid id)
    {
        await using var db = factory.CreateDatabaseContext();
        var i = await db.Incidents.AsNoTracking().SingleAsync(i => i.Id == id);
        return new StateSnapshot(i.Status, i.Version, i.DestinationHospitalId, i.ChiefComplaint,
            i.PatientAgeRange, i.PatientSex, i.Notes, i.UpdatedAt, i.UpdatedAtUtc, i.HandedOffAt,
            await db.AuditEvents.CountAsync(a => a.IncidentId == id),
            await db.VitalSigns.CountAsync(v => v.IncidentId == id),
            await db.Interventions.CountAsync(v => v.IncidentId == id));
    }

    private sealed record StateSnapshot(IncidentStatus Status, Guid Version, Guid? DestinationHospitalId,
        string ChiefComplaint, string? PatientAgeRange, string? PatientSex, string? Notes,
        DateTimeOffset UpdatedAt, DateTime UpdatedAtUtc, DateTimeOffset? HandedOffAt,
        int Audits, int Vitals, int Interventions);
}
