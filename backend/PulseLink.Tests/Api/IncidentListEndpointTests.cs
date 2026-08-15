using Microsoft.EntityFrameworkCore;
using PulseLink.Api.Dtos;
using PulseLink.Api.Services;
using PulseLink.Core.Entities;
using PulseLink.Core.Enums;
using PulseLink.Infrastructure.Data;

namespace PulseLink.Tests.Api;

public class IncidentListEndpointTests
{
    [Fact]
    public async Task HospitalStaff_SeesOnlyTransportingArrivedHandedOff_ForTheirHospital()
    {
        await using var harness = await CreateSqliteAsync();
        var data = await SeedAsync(harness.Db);
        var hospital = IncidentControllerFactory.Create(
            harness.Db, AppRoles.HospitalStaff, "hospital-metro", hospitalId: data.MetroHospital.Id);

        var page = await IncidentControllerFactory.ListAsync(hospital, pageSize: 100);

        Assert.Contains(page.Items, i => i.Id == data.MetroTransporting.Id);
        Assert.DoesNotContain(page.Items, i => i.Id == data.MetroDraft.Id);
        Assert.DoesNotContain(page.Items, i => i.Id == data.RuralTransporting.Id);
        Assert.All(page.Items, i => Assert.True(
            i.Status is IncidentStatus.Transporting or IncidentStatus.Arrived or IncidentStatus.HandedOff));
    }

    [Fact]
    public async Task Admin_SeesIncidentsAcrossAgenciesAndStatuses()
    {
        await using var harness = await CreateSqliteAsync();
        var data = await SeedAsync(harness.Db);
        var admin = IncidentControllerFactory.Create(harness.Db, AppRoles.Admin, "admin-1");

        var page = await IncidentControllerFactory.ListAsync(admin, pageSize: 100);

        Assert.Contains(page.Items, i => i.Id == data.MetroDraft.Id);
        Assert.Contains(page.Items, i => i.Id == data.RuralTransporting.Id);
        Assert.True(page.TotalCount >= 4);
    }

    [Fact]
    public async Task Paramedic_DoesNotSeeUnrelatedAgencyIncidents()
    {
        await using var harness = await CreateSqliteAsync();
        var data = await SeedAsync(harness.Db);
        var paramedic = IncidentControllerFactory.Create(
            harness.Db, AppRoles.Paramedic, data.MetroParamedicId, agencyId: data.MetroAgency.Id);

        var page = await IncidentControllerFactory.ListAsync(paramedic, pageSize: 100);

        Assert.Contains(page.Items, i => i.Id == data.MetroDraft.Id);
        Assert.Contains(page.Items, i => i.Id == data.CrossAgencyCreatedByMetro.Id);
        Assert.DoesNotContain(page.Items, i => i.Id == data.RuralTransporting.Id);
    }

    [Fact]
    public async Task List_Page1_ReturnsNewestSlice()
    {
        await using var harness = await CreateSqliteAsync();
        var data = await SeedPagingAsync(harness.Db);
        var admin = IncidentControllerFactory.Create(harness.Db, AppRoles.Admin, "admin-1");

        var page = await IncidentControllerFactory.ListAsync(admin, page: 1, pageSize: 10);

        Assert.Equal(1, page.Page);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(data.OrderedIds.Count, page.TotalCount);
        Assert.Equal(10, page.Items.Count);
        Assert.Equal(data.OrderedIds.Take(10), page.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task List_DeepPage_ReturnsTheCorrectSlice()
    {
        await using var harness = await CreateSqliteAsync();
        var data = await SeedPagingAsync(harness.Db);
        var admin = IncidentControllerFactory.Create(harness.Db, AppRoles.Admin, "admin-1");

        var page = await IncidentControllerFactory.ListAsync(admin, page: 3, pageSize: 10);

        Assert.Equal(3, page.Page);
        Assert.Equal(data.OrderedIds.Count, page.TotalCount);
        Assert.Equal(data.OrderedIds.Skip(20).Take(10), page.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task List_PageSizeAboveMax_IsClamped()
    {
        await using var harness = await CreateSqliteAsync();
        await SeedPagingAsync(harness.Db);
        var admin = IncidentControllerFactory.Create(harness.Db, AppRoles.Admin, "admin-1");

        var page = await IncidentControllerFactory.ListAsync(admin, page: 1, pageSize: 250);

        Assert.Equal(IncidentListQuery.MaxPageSize, page.PageSize);
        Assert.True(page.Items.Count <= IncidentListQuery.MaxPageSize);
    }

    [Fact]
    public async Task List_OrdersByUpdatedAtUtcThenIdDescending()
    {
        await using var harness = await CreateSqliteAsync();
        var expected = await SeedOrderingAsync(harness.Db);
        var admin = IncidentControllerFactory.Create(harness.Db, AppRoles.Admin, "admin-1");

        var page = await IncidentControllerFactory.ListAsync(admin, pageSize: 10);

        Assert.Equal(expected, page.Items.Select(i => i.Id));
        AssertMonotonicOrder(page.Items);
    }

    [Fact]
    public async Task Detail_ReturnsAgencyHospitalVitalsInterventionsAndAudits()
    {
        await using var harness = await CreateSqliteAsync();
        var data = await SeedAsync(harness.Db);
        var paramedic = IncidentControllerFactory.Create(
            harness.Db, AppRoles.Paramedic, data.MetroParamedicId, agencyId: data.MetroAgency.Id);

        var detail = await IncidentControllerFactory.GetAsync(paramedic, data.DetailIncident.Id);

        Assert.Equal(data.MetroAgency.Name, detail.AgencyName);
        Assert.Equal(data.MetroHospital.Name, detail.DestinationHospitalName);
        Assert.Equal(2, detail.VitalSigns.Count);
        Assert.Single(detail.Interventions);
        Assert.Equal(2, detail.AuditEvents.Count);
        Assert.Contains(detail.VitalSigns, v => v.HeartRate == 88);
        Assert.Contains(detail.Interventions, i => i.Name == "Oxygen");
        Assert.Contains(detail.AuditEvents, a => a.Action == "IncidentCreated");
    }

    internal static void AssertMonotonicOrder(IReadOnlyList<IncidentSummaryDto> items)
    {
        for (var i = 1; i < items.Count; i++)
        {
            var prev = items[i - 1];
            var cur = items[i];
            var cmp = prev.UpdatedAt.UtcDateTime.CompareTo(cur.UpdatedAt.UtcDateTime);
            Assert.True(cmp > 0 || cmp == 0, "List must be UpdatedAtUtc descending.");
            if (cmp == 0)
            {
                Assert.True(prev.Id.CompareTo(cur.Id) > 0, "Equal UpdatedAtUtc must break ties by Id descending.");
            }
        }
    }

    internal static async Task<IReadOnlyList<Guid>> SeedOrderingAsync(PulseLinkDbContext db)
    {
        var agency = new Agency { Id = Guid.NewGuid(), Name = "Order EMS", Region = "Test" };
        db.Agencies.Add(agency);

        var stamp = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var lowId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
        var highId = Guid.Parse("aaaaaaaa-0000-4000-8000-0000000000ff");

        var newest = IncidentControllerFactory.Incident(agency.Id, "u", stamp.AddHours(2), "ORD-NEW");
        var older = IncidentControllerFactory.Incident(agency.Id, "u", stamp, "ORD-OLD");
        var tieHigh = IncidentControllerFactory.Incident(agency.Id, "u", stamp.AddHours(1), "ORD-TIE-H", id: highId);
        var tieLow = IncidentControllerFactory.Incident(agency.Id, "u", stamp.AddHours(1), "ORD-TIE-L", id: lowId);

        db.Incidents.AddRange(older, newest, tieLow, tieHigh);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return [newest.Id, highId, lowId, older.Id];
    }

    private static async Task<SqliteHarness> CreateSqliteAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pulselink-list-{Guid.NewGuid():N}.db");
        var db = new SqlitePulseLinkDbContext(
            new DbContextOptionsBuilder<SqlitePulseLinkDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options);
        await db.Database.MigrateAsync();
        return new SqliteHarness(db, path);
    }

    private static async Task<Seed> SeedAsync(PulseLinkDbContext db)
    {
        var metroAgency = new Agency { Id = Guid.NewGuid(), Name = "Metro EMS", Region = "Metro" };
        var ruralAgency = new Agency { Id = Guid.NewGuid(), Name = "Rural EMS", Region = "Rural" };
        var metroHospital = new Hospital { Id = Guid.NewGuid(), Name = "Metro General", City = "Metro" };
        var ruralHospital = new Hospital { Id = Guid.NewGuid(), Name = "Rural Clinic", City = "Rural" };
        db.Agencies.AddRange(metroAgency, ruralAgency);
        db.Hospitals.AddRange(metroHospital, ruralHospital);

        const string metroParamedic = "paramedic-metro";
        const string ruralParamedic = "paramedic-rural";
        var now = DateTimeOffset.UtcNow;

        var metroDraft = IncidentControllerFactory.Incident(
            metroAgency.Id, metroParamedic, now.AddMinutes(-10), "ROLE-DRAFT", IncidentStatus.Draft, metroHospital.Id);
        var metroTransporting = IncidentControllerFactory.Incident(
            metroAgency.Id, metroParamedic, now.AddMinutes(-8), "ROLE-TX", IncidentStatus.Transporting, metroHospital.Id);
        var ruralTransporting = IncidentControllerFactory.Incident(
            ruralAgency.Id, ruralParamedic, now.AddMinutes(-6), "ROLE-RURAL-TX", IncidentStatus.Transporting, ruralHospital.Id);
        var crossCreate = IncidentControllerFactory.Incident(
            ruralAgency.Id, metroParamedic, now.AddMinutes(-4), "ROLE-CROSS", IncidentStatus.Draft, ruralHospital.Id);
        var detail = IncidentControllerFactory.Incident(
            metroAgency.Id, metroParamedic, now.AddMinutes(-2), "ROLE-DETAIL", IncidentStatus.OnScene, metroHospital.Id);

        db.Incidents.AddRange(metroDraft, metroTransporting, ruralTransporting, crossCreate, detail);
        db.VitalSigns.AddRange(
            new VitalSign { Id = Guid.NewGuid(), IncidentId = detail.Id, RecordedAt = now.AddMinutes(-3), HeartRate = 88, SpO2 = 97.5m },
            new VitalSign { Id = Guid.NewGuid(), IncidentId = detail.Id, RecordedAt = now.AddMinutes(-1), HeartRate = 92, SpO2 = 96.0m });
        db.Interventions.Add(new Intervention
        {
            Id = Guid.NewGuid(),
            IncidentId = detail.Id,
            PerformedAt = now.AddMinutes(-2),
            Name = "Oxygen",
            Route = "NC"
        });
        db.AuditEvents.AddRange(
            new AuditEvent { Id = Guid.NewGuid(), IncidentId = detail.Id, ActorUserId = metroParamedic, Action = "IncidentCreated", Details = "seed", CreatedAt = now.AddMinutes(-5) },
            new AuditEvent { Id = Guid.NewGuid(), IncidentId = detail.Id, ActorUserId = metroParamedic, Action = "StatusChanged", Details = "OnScene", CreatedAt = now.AddMinutes(-2) });

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return new Seed(metroAgency, ruralAgency, metroHospital, ruralHospital, metroParamedic,
            metroDraft, metroTransporting, ruralTransporting, crossCreate, detail);
    }

    private static async Task<PagingSeed> SeedPagingAsync(PulseLinkDbContext db)
    {
        var agency = new Agency { Id = Guid.NewGuid(), Name = "Page EMS", Region = "Test" };
        db.Agencies.Add(agency);

        var origin = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        var rows = new List<Incident>(25);
        for (var i = 0; i < 25; i++)
        {
            rows.Add(IncidentControllerFactory.Incident(
                agency.Id, "pager", origin.AddMinutes(i), $"PAGE-{i:D2}"));
        }

        db.Incidents.AddRange(rows);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var ordered = rows
            .OrderByDescending(r => r.UpdatedAtUtc)
            .ThenByDescending(r => r.Id)
            .Select(r => r.Id)
            .ToList();
        return new PagingSeed(ordered);
    }

    private sealed record Seed(
        Agency MetroAgency,
        Agency RuralAgency,
        Hospital MetroHospital,
        Hospital RuralHospital,
        string MetroParamedicId,
        Incident MetroDraft,
        Incident MetroTransporting,
        Incident RuralTransporting,
        Incident CrossAgencyCreatedByMetro,
        Incident DetailIncident);

    private sealed record PagingSeed(IReadOnlyList<Guid> OrderedIds);

    private sealed class SqliteHarness(SqlitePulseLinkDbContext db, string path) : IAsyncDisposable
    {
        public SqlitePulseLinkDbContext Db { get; } = db;

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            try { File.Delete(path); } catch { /* temp */ }
        }
    }
}
