using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PulseLink.Core.Entities;
using PulseLink.Core.Enums;
using PulseLink.Tests.Infrastructure;

namespace PulseLink.Tests.Api;

public class InterventionRetryHttpTests
{
    [Fact]
    public Task LostResponseAfterCommit_Sqlite() => CheckLostResponseAsync(false);

    [LocalDbFact]
    public Task LostResponseAfterCommit_SqlServer() => CheckLostResponseAsync(true);

    private static async Task CheckLostResponseAsync(bool sqlServer)
    {
        await using var factory = new IncidentApiFactory(sqlServer);
        var id = await factory.SeedIncidentAsync(IncidentStatus.Arrived, factory.HospitalA);
        using var client = factory.CreateDefaultClient(new LoseFirstResponse());
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.ActorHeader, "paramedic-a");
        var version = await client.ReadVersionAsync(id);
        var key = Guid.NewGuid();
        await Assert.ThrowsAsync<HttpRequestException>(() => SendAsync(client, id, version, key, new { name = "Test" }));
        using var retry = await SendAsync(client, id, version, key, new { name = "Test" });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var result = await retry.Content.ReadFromJsonAsync<JsonElement>();
        await using var db = factory.CreateDatabaseContext();
        var intervention = Assert.Single(await db.Interventions.ToListAsync());
        Assert.Equal(intervention.Id, result.GetProperty("interventionId").GetGuid());
        Assert.Equal(1, await db.AuditEvents.CountAsync());
        Assert.Equal(1, await db.InterventionOperations.CountAsync());
    }

    private sealed class LoseFirstResponse : DelegatingHandler
    {
        private int lose = 1;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (request.Method == HttpMethod.Post && response.IsSuccessStatusCode
                && Interlocked.Exchange(ref lose, 0) == 1)
            {
                response.Dispose();
                throw new HttpRequestException("Synthetic lost response after the API committed.");
            }
            return response;
        }
    }

    [Fact]
    public Task ReplayContract_Sqlite() => CheckReplayContractAsync(false);

    [LocalDbFact]
    public Task ReplayContract_SqlServer() => CheckReplayContractAsync(true);

    private static async Task CheckReplayContractAsync(bool sqlServer)
    {
        await using var factory = new IncidentApiFactory(sqlServer);
        var id = await factory.SeedIncidentAsync(IncidentStatus.Arrived, factory.HospitalA);
        using var client = factory.CreateClientAs("paramedic-a");
        var version = await client.ReadVersionAsync(id);
        var key = Guid.NewGuid();
        using var saved = await SendAsync(client, id, version, key, new { name = "Test" });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var originalResult = await saved.Content.ReadAsStringAsync();

        // The original response can be lost; a fresh application host must use
        // the database record, including after another request hands the incident off.
        using var handoff = await client.PostIncidentAsync($"/api/incidents/{id}/status", new { toStatus = "HandedOff" });
        Assert.Equal(HttpStatusCode.OK, handoff.StatusCode);
        var afterHandoff = await client.ReadVersionAsync(id);
        await using var restartedHost = factory.WithWebHostBuilder(_ => { });
        using var restartedClient = restartedHost.CreateClient();
        restartedClient.DefaultRequestHeaders.Add(TestAuthenticationHandler.ActorHeader, "paramedic-a");
        using var replay = await SendAsync(restartedClient, id, version, key, new { name = " Test " });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(originalResult, await replay.Content.ReadAsStringAsync());
        Assert.Equal(afterHandoff, await client.ReadVersionAsync(id));

        foreach (var body in new object[] {
            new { name = "Different" }, new { name = "Test", medication = "Different" },
            new { name = "Test", dose = "Different" }, new { name = "Test", route = "Different" },
            new { name = "Test", notes = "Different" }, new { name = "Test", notes = "" } })
        {
            using var mismatch = await SendAsync(client, id, version, key, body);
            await AssertErrorAsync(mismatch, HttpStatusCode.Conflict, "idempotency_key_reused");
        }

        using var unrelated = factory.CreateClientAs("paramedic-b");
        using var denied = await SendAsync(unrelated, id, version, key, new { name = "Test" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var hospital = factory.CreateClientAs("hospital-a");
        using var deniedHospital = await SendAsync(hospital, id, version, key, new { name = "Test" });
        Assert.Equal(HttpStatusCode.Forbidden, deniedHospital.StatusCode);
        using var admin = factory.CreateClientAs("admin");
        using var otherActor = await SendAsync(admin, id, version, key, new { name = "Test" });
        Assert.Equal(HttpStatusCode.PreconditionFailed, otherActor.StatusCode);

        await using (var db = factory.CreateDatabaseContext())
        {
            var operation = Assert.Single(await db.InterventionOperations.ToListAsync());
            Assert.Equal("paramedic-a", operation.ActorUserId);
            Assert.Equal(TimeSpan.FromHours(24), operation.ExpiresAtUtc - operation.CompletedAtUtc);
            Assert.Equal(1, await db.Interventions.CountAsync());
            Assert.Equal(2, await db.AuditEvents.CountAsync());
            operation.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        using var expired = await SendAsync(client, id, afterHandoff, key, new { name = "Test" });
        await AssertErrorAsync(expired, HttpStatusCode.Gone, "idempotency_key_expired");
        await using var verify = factory.CreateDatabaseContext();
        Assert.Equal(1, await verify.InterventionOperations.CountAsync());
        Assert.Equal(1, await verify.Interventions.CountAsync());
        Assert.Equal(afterHandoff, await client.ReadVersionAsync(id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ConcurrentDuplicates_Sqlite(bool differentPayload) => CheckConcurrentAsync(false, differentPayload);

    [LocalDbFact]
    public async Task ConcurrentDuplicates_SqlServer()
    {
        await CheckConcurrentAsync(true, false);
        await CheckConcurrentAsync(true, true);
    }

    private static async Task CheckConcurrentAsync(bool sqlServer, bool differentPayload)
    {
        var gate = new FirstSaveGate();
        await using var factory = new IncidentApiFactory(sqlServer, gate);
        var id = await factory.SeedIncidentAsync(IncidentStatus.Arrived, factory.HospitalA);
        using var client = factory.CreateClientAs("paramedic-a");
        var version = await client.ReadVersionAsync(id);
        var key = Guid.NewGuid();
        gate.Arm();
        var firstTask = SendAsync(client, id, version, key, new { name = differentPayload ? "Other" : "Test" });
        string result, winningVersion;
        try
        {
            await gate.Paused.Task.WaitAsync(TimeSpan.FromSeconds(20));
            using var second = await SendAsync(client, id, version, key, new { name = "Test" });
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            result = await second.Content.ReadAsStringAsync();
            winningVersion = await client.ReadVersionAsync(id);
        }
        finally { gate.Release.TrySetResult(); }
        using var first = await firstTask;
        if (differentPayload)
            await AssertErrorAsync(first, HttpStatusCode.Conflict, "idempotency_key_reused");
        else
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(result, await first.Content.ReadAsStringAsync());
        }
        Assert.Equal(winningVersion, await client.ReadVersionAsync(id));
        await using var db = factory.CreateDatabaseContext();
        Assert.Equal(1, await db.InterventionOperations.CountAsync());
        Assert.Equal(1, await db.Interventions.CountAsync());
        Assert.Equal(1, await db.AuditEvents.CountAsync());
    }

    [Fact]
    public Task TransactionAndUniqueConstraint_Sqlite() => CheckAtomicityAsync(false);

    [LocalDbFact]
    public Task TransactionAndUniqueConstraint_SqlServer() => CheckAtomicityAsync(true);

    private static async Task CheckAtomicityAsync(bool sqlServer)
    {
        var failure = new FailAfterOperationInsert();
        await using var factory = new IncidentApiFactory(sqlServer, failure);
        var id = await factory.SeedIncidentAsync(IncidentStatus.Arrived, factory.HospitalA);
        using var client = factory.CreateClientAs("paramedic-a");
        var version = await client.ReadVersionAsync(id);
        var key = Guid.NewGuid();
        await Assert.ThrowsAsync<DbUpdateException>(() => SendAsync(client, id, version, key, new { name = "Test" }));
        await using (var db = factory.CreateDatabaseContext())
        {
            Assert.Equal(0, await db.Interventions.CountAsync());
            Assert.Equal(0, await db.InterventionOperations.CountAsync());
            Assert.Equal(0, await db.AuditEvents.CountAsync());
            Assert.Equal(Guid.Parse(version), (await db.Incidents.SingleAsync()).Version);
        }
        // A failed transaction did not consume the key.
        using var retry = await SendAsync(client, id, version, key, new { name = "Test" });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        await using var duplicate = factory.CreateDatabaseContext();
        var original = await duplicate.InterventionOperations.AsNoTracking().SingleAsync();
        original.Id = Guid.NewGuid();
        duplicate.InterventionOperations.Add(original);
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
        await using var verify = factory.CreateDatabaseContext();
        Assert.Equal(1, await verify.InterventionOperations.CountAsync());
        Assert.Equal(1, await verify.Interventions.CountAsync());
        Assert.Equal(1, await verify.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task InvalidRequest_DoesNotConsumeKey_AndKeysAreScopedToIncidentAndActor()
    {
        await using var factory = new IncidentApiFactory();
        var id = await factory.SeedIncidentAsync(IncidentStatus.Arrived, factory.HospitalA);
        using var client = factory.CreateClientAs("paramedic-a");
        var version = await client.ReadVersionAsync(id);
        var key = Guid.NewGuid();
        using var invalid = await SendAsync(client, id, version, key, new { name = "" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var stale = await SendAsync(client, id, Guid.NewGuid().ToString(), key, new { name = "Test" });
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        await using (var db = factory.CreateDatabaseContext()) Assert.Equal(0, await db.InterventionOperations.CountAsync());
        using var valid = await SendAsync(client, id, version, key, new { name = "Test" });
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        var other = await factory.SeedIncidentAsync(IncidentStatus.Arrived, factory.HospitalB);
        using var otherIncident = await SendAsync(client, other, await client.ReadVersionAsync(other), key, new { name = "Test" });
        Assert.Equal(HttpStatusCode.OK, otherIncident.StatusCode);
        using var admin = factory.CreateClientAs("admin");
        using var otherActor = await SendAsync(admin, id, await admin.ReadVersionAsync(id), key, new { name = "Test" });
        Assert.Equal(HttpStatusCode.OK, otherActor.StatusCode);
        await using var verify = factory.CreateDatabaseContext();
        Assert.Equal(3, await verify.Interventions.CountAsync());
        Assert.Equal(3, await verify.InterventionOperations.CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidKey_IsRejected(string? key)
    {
        await using var factory = new IncidentApiFactory();
        var id = await factory.SeedIncidentAsync(IncidentStatus.Arrived, factory.HospitalA);
        using var client = factory.CreateClientAs("paramedic-a");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/incidents/{id}/interventions")
        {
            Content = JsonContent.Create(new { name = "Test" })
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{await client.ReadVersionAsync(id)}\"");
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        using var response = await client.SendAsync(request);
        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "idempotency_key_required");
        await using var db = factory.CreateDatabaseContext();
        Assert.Equal(0, await db.InterventionOperations.CountAsync());
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, error.GetProperty("code").GetString());
    }

    private sealed class FirstSaveGate : SaveChangesInterceptor
    {
        private int armed;
        public TaskCompletionSource Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Arm() => armed = 1;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<InterventionOperation>().Any(e => e.State == EntityState.Added)
                && Interlocked.Exchange(ref armed, 0) == 1)
            {
                Paused.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    private sealed class FailAfterOperationInsert : DbCommandInterceptor
    {
        private int fail = 1;
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO", StringComparison.Ordinal)
                && command.CommandText.Contains("InterventionOperations", StringComparison.Ordinal)
                && Interlocked.Exchange(ref fail, 0) == 1)
            {
                // The database has executed the insert; fail before EF can finish
                // SaveChanges and commit, proving rollback of actual writes.
                await result.DisposeAsync();
                throw new InvalidOperationException("Synthetic failure after operation insert, before commit.");
            }
            return result;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameKey_RetryReturnsSuccessWithoutDuplicate(bool reloadVersion)
    {
        await using var factory = new IncidentApiFactory();
        var id = await factory.SeedIncidentAsync(IncidentStatus.Arrived, factory.HospitalA);
        using var client = factory.CreateClientAs("paramedic-a");
        var version = await client.ReadVersionAsync(id);
        var key = Guid.NewGuid();
        using var first = await SendAsync(client, id, version, key, new { name = "Synthetic intervention" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        if (reloadVersion) version = await client.ReadVersionAsync(id);
        using var retry = await SendAsync(client, id, version, key, new { name = "Synthetic intervention" });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        await using var db = factory.CreateDatabaseContext();
        Assert.Equal(1, await db.Interventions.CountAsync());
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == "InterventionAdded"));
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, Guid id,
        string version, Guid key, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/incidents/{id}/interventions")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        request.Headers.Add("Idempotency-Key", key.ToString());
        return await client.SendAsync(request);
    }
}
