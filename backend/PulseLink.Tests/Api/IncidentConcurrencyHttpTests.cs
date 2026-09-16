using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Data.Sqlite;
using PulseLink.Core.Entities;
using PulseLink.Core.Enums;
using PulseLink.Tests.Infrastructure;

namespace PulseLink.Tests.Api;

public class IncidentConcurrencyHttpTests
{
    [Theory]
    [InlineData("edit")]
    [InlineData("vitals")]
    [InlineData("interventions")]
    [InlineData("status")]
    public Task StaleClient_CannotCommit(string operation) => CheckStaleClientAsync(false, operation);

    [LocalDbFact]
    public async Task SqlServer_StaleClients_CannotCommit()
    {
        foreach (var operation in new[] { "edit", "vitals", "interventions", "status" })
            await CheckStaleClientAsync(true, operation);
    }

    private static async Task CheckStaleClientAsync(bool sqlServer, string operation)
    {
        await using var factory = new IncidentApiFactory(sqlServer);
        var id = await factory.SeedIncidentAsync(IncidentStatus.Arrived, factory.HospitalA);
        using var first = factory.CreateClientAs("paramedic-a");
        using var second = factory.CreateClientAs("paramedic-a");
        var original = await first.ReadVersionAsync(id);
        Assert.Equal(original, await second.ReadVersionAsync(id));
        using var winner = await WriteAsync(first, id, factory.HospitalA, "edit", original);
        Assert.Equal(HttpStatusCode.OK, winner.StatusCode);
        var next = await first.ReadVersionAsync(id);
        Assert.NotEqual(original, next);

        using var stale = await WriteAsync(second, id, factory.HospitalA, operation, original);
        await AssertConflictAsync(stale);
        Assert.Equal(next, await first.ReadVersionAsync(id));
        await using var db = factory.CreateDatabaseContext();
        Assert.Equal(1, await db.AuditEvents.CountAsync());
        Assert.Equal(0, await db.VitalSigns.CountAsync());
        Assert.Equal(0, await db.Interventions.CountAsync());
        Assert.Equal(IncidentStatus.Arrived, (await db.Incidents.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("edit")]
    [InlineData("vitals")]
    [InlineData("interventions")]
    [InlineData("status")]
    public Task RacingRequests_OnlyWinnerCommits(string losingOperation) => CheckRaceAsync(false, losingOperation);

    [LocalDbFact]
    public async Task SqlServer_RacingRequests_OnlyWinnerCommits()
    {
        foreach (var operation in new[] { "edit", "vitals", "interventions", "status" })
            await CheckRaceAsync(true, operation);
    }

    private static async Task CheckRaceAsync(bool sqlServer, string losingOperation)
    {
        var gate = new PauseFirstIncidentSave();
        await using var factory = new IncidentApiFactory(sqlServer, gate);
        var id = await factory.SeedIncidentAsync(IncidentStatus.Arrived, factory.HospitalA);
        using var loser = factory.CreateClientAs("paramedic-a");
        using var winner = factory.CreateClientAs("admin");
        var original = await loser.ReadVersionAsync(id);
        gate.Arm();
        var losingRequest = WriteAsync(loser, id, factory.HospitalA, losingOperation, original);
        var winningOperation = losingOperation == "status" ? "interventions" : "status";
        try
        {
            await gate.Paused.Task.WaitAsync(TimeSpan.FromSeconds(20));
            using var winningResponse = await WriteAsync(winner, id, factory.HospitalA, winningOperation, original);
            Assert.Equal(HttpStatusCode.OK, winningResponse.StatusCode);
        }
        finally
        {
            gate.Release.TrySetResult();
        }
        using var losingResponse = await losingRequest;
        await AssertConflictAsync(losingResponse);
        await using var db = factory.CreateDatabaseContext();
        var incident = await db.Incidents.SingleAsync();
        Assert.NotEqual(Guid.Parse(original), incident.Version);
        Assert.Equal("Test", incident.ChiefComplaint);
        Assert.Equal(losingOperation == "status" ? IncidentStatus.Arrived : IncidentStatus.HandedOff, incident.Status);
        Assert.Equal(0, await db.VitalSigns.CountAsync());
        Assert.Equal(losingOperation == "status" ? 1 : 0, await db.Interventions.CountAsync());
        Assert.Equal(losingOperation == "status" ? 1 : 0, await db.InterventionOperations.CountAsync());
        var audit = Assert.Single(await db.AuditEvents.ToListAsync());
        Assert.Equal("admin", audit.ActorUserId);
        Assert.Equal(losingOperation == "status" ? "InterventionAdded" : "StatusChanged", audit.Action);

        // Reloading after a winning handoff cannot make a clinical write valid.
        if (losingOperation != "status")
        {
            using var retried = await WriteAsync(loser, id, factory.HospitalA, losingOperation, incident.Version.ToString());
            Assert.Equal(HttpStatusCode.BadRequest, retried.StatusCode);
        }
    }

    [Theory]
    [InlineData("*")]
    [InlineData("invalid")]
    [InlineData("W/\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("\"00000000-0000-0000-0000-000000000000\", \"00000000-0000-0000-0000-000000000001\"")]
    public async Task InvalidIfMatch_IsRejected(string header)
    {
        await using var factory = new IncidentApiFactory();
        var id = await factory.SeedIncidentAsync(IncidentStatus.Arrived, factory.HospitalA);
        using var client = factory.CreateClientAs("paramedic-a");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/incidents/{id}/vitals")
        {
            Content = JsonContent.Create(new { heartRate = 80 })
        };
        request.Headers.TryAddWithoutValidation("If-Match", header);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = factory.CreateDatabaseContext();
        Assert.Equal(0, await db.AuditEvents.CountAsync());
        Assert.Equal(0, await db.VitalSigns.CountAsync());
    }

    [Fact]
    public async Task EachClinicalWrite_AdvancesVersion_ExportDoesNot()
    {
        await using var factory = new IncidentApiFactory();
        var id = await factory.SeedIncidentAsync(IncidentStatus.Arrived, factory.HospitalA);
        using var client = factory.CreateClientAs("paramedic-a");
        var version = await client.ReadVersionAsync(id);
        foreach (var operation in new[] { "edit", "vitals", "interventions", "status" })
        {
            using var response = await WriteAsync(client, id, factory.HospitalA, operation, version);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var next = await client.ReadVersionAsync(id);
            Assert.NotEqual(version, next);
            version = next;
        }
        using var export = await client.GetAsync($"/api/incidents/{id}/export");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal(version, await client.ReadVersionAsync(id));
    }

    private static Task<HttpResponseMessage> WriteAsync(HttpClient client, Guid id, Guid hospitalId, string operation, string version) =>
        operation switch
        {
            "edit" => client.SendVersionAsync(HttpMethod.Put, $"/api/incidents/{id}", new { chiefComplaint = "Edited", destinationHospitalId = hospitalId }, version),
            "vitals" => client.SendVersionAsync(HttpMethod.Post, $"/api/incidents/{id}/vitals", new { heartRate = 80 }, version),
            "interventions" => client.SendVersionAsync(HttpMethod.Post, $"/api/incidents/{id}/interventions", new { name = "Test intervention" }, version),
            "status" => client.SendVersionAsync(HttpMethod.Post, $"/api/incidents/{id}/status", new { toStatus = "HandedOff" }, version),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

    private static async Task AssertConflictAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("incident_conflict", error.GetProperty("code").GetString());
    }

    private sealed class PauseFirstIncidentSave : SaveChangesInterceptor
    {
        private int armed;
        public TaskCompletionSource Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Arm() => armed = 1;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Incident>().Any(e => e.State == EntityState.Modified)
                && Interlocked.Exchange(ref armed, 0) == 1)
            {
                Paused.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    [Theory]
    [InlineData("edit")]
    [InlineData("vitals")]
    [InlineData("interventions")]
    [InlineData("status")]
    public async Task Write_WithoutVersion_IsRejected(string operation)
    {
        await using var factory = new IncidentApiFactory();
        var id = await factory.SeedIncidentAsync(IncidentStatus.Arrived, factory.HospitalA);
        using var client = factory.CreateClientAs("paramedic-a");
        using var request = new HttpRequestMessage(operation == "edit" ? HttpMethod.Put : HttpMethod.Post,
            $"/api/incidents/{id}" + (operation == "edit" ? "" : $"/{operation}"))
        {
            Content = JsonContent.Create(new { chiefComplaint = "Stale edit", destinationHospitalId = factory.HospitalA,
                name = "Test intervention", heartRate = 80, toStatus = "HandedOff" })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);
        Assert.Equal((HttpStatusCode)428, response.StatusCode);
        await using var db = factory.CreateDatabaseContext();
        Assert.Equal(0, await db.AuditEvents.CountAsync());
    }

    [Fact]
    public Task Sqlite_Migration_InitializesExistingVersions() => CheckMigrationAsync(false);

    [LocalDbFact]
    public Task SqlServer_Migration_InitializesExistingVersions() => CheckMigrationAsync(true);

    private static async Task CheckMigrationAsync(bool sqlServer)
    {
        await using var factory = new IncidentApiFactory(sqlServer);
        var first = await factory.SeedIncidentAsync(IncidentStatus.Arrived, factory.HospitalA);
        var second = await factory.SeedIncidentAsync(IncidentStatus.Draft, factory.HospitalB);
        await using (var db = factory.CreateDatabaseContext())
        {
            var migrator = db.GetService<IMigrator>();
            // Downgrade ONLY this disposable test database to simulate existing data
            // without a Version column, then apply the new migration.
            await migrator.MigrateAsync(sqlServer ? "20260814231449_AddListAccessIndexes" : "20260814231436_AddListAccessIndexes");
            await migrator.MigrateAsync();
        }
        await using var verify = factory.CreateDatabaseContext();
        var rows = await verify.Incidents.OrderBy(i => i.IncidentNumber).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, i => i.Id == first && i.Status == IncidentStatus.Arrived);
        Assert.Contains(rows, i => i.Id == second && i.Status == IncidentStatus.Draft);
        Assert.All(rows, i => Assert.NotEqual(Guid.Empty, i.Version));
        Assert.NotEqual(rows[0].Version, rows[1].Version);
        Assert.False(verify.Database.HasPendingModelChanges());
        var migratedVersion = rows[0].Version;
        rows[0].ChiefComplaint = "Edited after migration";
        await verify.SaveChangesAsync();
        Assert.NotEqual(migratedVersion, rows[0].Version);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SqliteWriteContention_ReturnsControlledRetryableError(bool wrapped)
    {
        await using var factory = new IncidentApiFactory(false, new BusySave(wrapped));
        var id = await factory.SeedIncidentAsync(IncidentStatus.Arrived, factory.HospitalA);
        using var client = factory.CreateClientAs("paramedic-a");
        var version = await client.ReadVersionAsync(id);
        using var response = await WriteAsync(client, id, factory.HospitalA, "interventions", version);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("incident_busy", error.GetProperty("code").GetString());
        Assert.Equal(version, await client.ReadVersionAsync(id));
        await using var db = factory.CreateDatabaseContext();
        Assert.Equal(0, await db.Interventions.CountAsync());
        Assert.Equal(0, await db.AuditEvents.CountAsync());
    }

    private sealed class BusySave(bool wrapped) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!eventData.Context!.ChangeTracker.Entries<Incident>().Any(e => e.State == EntityState.Modified))
                return ValueTask.FromResult(result);
            var busy = new SqliteException("Synthetic write contention", 5);
            throw wrapped ? new DbUpdateException("Synthetic save failure", busy) : busy;
        }
    }

    [Fact]
    public async Task SeparateContexts_HandoffWinner_RollsBackLosingClinicalWrite()
    {
        await using var factory = new IncidentApiFactory();
        var id = await factory.SeedIncidentAsync(IncidentStatus.Arrived, factory.HospitalA);
        await using var winner = factory.CreateDatabaseContext();
        await using var loser = factory.CreateDatabaseContext();
        // Both reads finish before either save. No timing-dependent sleep.
        var first = await winner.Incidents.SingleAsync(i => i.Id == id);
        var second = await loser.Incidents.SingleAsync(i => i.Id == id);
        first.Status = IncidentStatus.HandedOff;
        first.HandedOffAt = DateTimeOffset.UtcNow;
        await winner.SaveChangesAsync();

        second.ChiefComplaint = "Losing edit";
        loser.Interventions.Add(new Intervention { Id = Guid.NewGuid(), IncidentId = id, Name = "Losing intervention" });
        loser.AuditEvents.Add(new AuditEvent { Id = Guid.NewGuid(), IncidentId = id, ActorUserId = "paramedic-a", Action = "LosingWrite", Details = "Test" });
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => loser.SaveChangesAsync());

        await using var verify = factory.CreateDatabaseContext();
        Assert.Equal("Test", (await verify.Incidents.SingleAsync(i => i.Id == id)).ChiefComplaint);
        Assert.Equal(0, await verify.Interventions.CountAsync());
        Assert.Equal(0, await verify.AuditEvents.CountAsync());
    }
}
