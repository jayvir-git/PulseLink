using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PulseLink.Core.Entities;
using PulseLink.Core.Enums;

namespace PulseLink.Tests.Api;

public class HandoffReceiptHttpTests
{
    [Theory]
    [InlineData("paramedic-a", IncidentStatus.HandedOff, HttpStatusCode.Forbidden)]
    [InlineData("admin", IncidentStatus.HandedOff, HttpStatusCode.Forbidden)]
    [InlineData("hospital-b", IncidentStatus.HandedOff, HttpStatusCode.Forbidden)]
    [InlineData("hospital-a", IncidentStatus.Arrived, HttpStatusCode.Conflict)]
    public async Task OnlyReceivingHospitalMayAcknowledgeCompletedReport(string actor, IncidentStatus status, HttpStatusCode expected)
    {
        await using var factory = new IncidentApiFactory();
        var id = await factory.SeedIncidentAsync(status, factory.HospitalA);
        using var client = factory.CreateClientAs(actor);
        using var response = await client.PutAsync($"/api/incidents/{id}/receipt", null);
        Assert.Equal(expected, response.StatusCode);
        using var medic = factory.CreateClientAs("paramedic-a");
        var detail = await medic.GetFromJsonAsync<JsonElement>($"/api/incidents/{id}");
        Assert.Empty(detail.GetProperty("auditEvents").EnumerateArray());
    }

    [Fact]
    public async Task Receipt_IsVisibleToEms_AndRepeatedPutReturnsOriginal()
    {
        var gate = new PauseFirstReceipt();
        await using var factory = new IncidentApiFactory(null, gate);
        var id = await factory.SeedIncidentAsync(IncidentStatus.HandedOff, factory.HospitalA);
        using var hospital = factory.CreateClientAs("hospital-a");
        using var medic = factory.CreateClientAs("paramedic-a");
        var path = $"/api/incidents/{id}";
        using var colleague = factory.CreateClientAs("hospital-colleague");
        var racingRequest = colleague.PutAsync(path + "/receipt", null);
        HttpResponseMessage first;
        try
        {
            await gate.Paused.Task.WaitAsync(TimeSpan.FromSeconds(20));
            first = await hospital.PutAsync(path + "/receipt", null);
        }
        finally { gate.Release.TrySetResult(); }
        using var firstResponse = first;
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var retry = await hospital.PutAsync(path + "/receipt", null);
        retry.EnsureSuccessStatusCode();
        var receipt = await first.Content.ReadFromJsonAsync<JsonElement>();
        using var racing = await racingRequest;
        racing.EnsureSuccessStatusCode();
        Assert.Equal(receipt.ToString(), (await racing.Content.ReadFromJsonAsync<JsonElement>()).ToString());
        Assert.Equal(receipt.ToString(), (await retry.Content.ReadFromJsonAsync<JsonElement>()).ToString());
        Assert.Equal("hospital-a", receipt.GetProperty("actorUserId").GetString());
        var detail = await medic.GetFromJsonAsync<JsonElement>(path);
        var entry = Assert.Single(detail.GetProperty("auditEvents").EnumerateArray());
        Assert.Equal(receipt.GetProperty("id").GetGuid(), entry.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task HospitalQueues_AreScoped_FilterBeforePaging_AndReflectReceipt()
    {
        await using var factory = new IncidentApiFactory();
        await factory.SeedIncidentAsync(IncidentStatus.Transporting, factory.HospitalA);
        await factory.SeedIncidentAsync(IncidentStatus.Arrived, factory.HospitalA);
        var awaiting = await factory.SeedIncidentAsync(IncidentStatus.HandedOff, factory.HospitalA);
        await factory.SeedIncidentAsync(IncidentStatus.HandedOff, factory.HospitalB);
        using var hospital = factory.CreateClientAs("hospital-a");
        var list = await hospital.GetFromJsonAsync<JsonElement>("/api/incidents?queue=awaiting&pageSize=1");
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        var row = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal(awaiting, row.GetProperty("id").GetGuid());
        Assert.False(row.GetProperty("receiptAcknowledged").GetBoolean());
        Assert.Equal(1, list.GetProperty("queueCounts").GetProperty("incoming").GetInt32());
        Assert.Equal(1, list.GetProperty("queueCounts").GetProperty("arrived").GetInt32());
        Assert.Equal(1, list.GetProperty("queueCounts").GetProperty("awaiting").GetInt32());
        using var receipt = await hospital.PutAsync($"/api/incidents/{awaiting}/receipt", null);
        receipt.EnsureSuccessStatusCode();
        var empty = await hospital.GetFromJsonAsync<JsonElement>("/api/incidents?queue=awaiting");
        Assert.Equal(0, empty.GetProperty("totalCount").GetInt32());
        var received = await hospital.GetFromJsonAsync<JsonElement>("/api/incidents?queue=received");
        Assert.True(Assert.Single(received.GetProperty("items").EnumerateArray()).GetProperty("receiptAcknowledged").GetBoolean());
        Assert.Equal(1, received.GetProperty("queueCounts").GetProperty("received").GetInt32());
        using var invalid = await hospital.GetAsync("/api/incidents?queue=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private sealed class PauseFirstReceipt : SaveChangesInterceptor
    {
        private int paused;
        public TaskCompletionSource Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AuditEvent>().Any(e => e.Entity.Action == AuditEvent.ReceiptAction)
                && Interlocked.Exchange(ref paused, 1) == 0)
            {
                Paused.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
