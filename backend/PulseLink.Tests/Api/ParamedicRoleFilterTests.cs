using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PulseLink.Api.Controllers;
using PulseLink.Api.Dtos;
using PulseLink.Core.Entities;
using PulseLink.Core.Enums;
using PulseLink.Infrastructure.Data;

namespace PulseLink.Tests.Api;

public class ParamedicRoleFilterTests
{
    [Fact]
    public async Task Paramedic_SeesIncidentsFromOwnAgency()
    {
        await using var harness = await Harness.CreateAsync();
        var page = await harness.ListAsync();

        Assert.Contains(page.Items, i => i.Id == harness.OwnAgencyCreatedBySelf);
        Assert.Contains(page.Items, i => i.Id == harness.OwnAgencyCreatedByOther);
    }

    [Fact]
    public async Task Paramedic_SeesIncidentsTheyCreatedAtAnotherAgency()
    {
        await using var harness = await Harness.CreateAsync();
        var page = await harness.ListAsync();

        Assert.Contains(page.Items, i => i.Id == harness.OtherAgencyCreatedBySelf);
    }

    [Fact]
    public async Task Paramedic_DoesNotSeeOtherAgencyIncidentsTheyDidNotCreate()
    {
        await using var harness = await Harness.CreateAsync();
        var page = await harness.ListAsync();

        Assert.DoesNotContain(page.Items, i => i.Id == harness.OtherAgencyCreatedByOther);
        Assert.DoesNotContain(page.Items, i => i.Id == harness.HiddenOtherAgency);
    }

    [Fact]
    public async Task Paramedic_IncidentMatchingBothPredicates_AppearsOnce()
    {
        await using var harness = await Harness.CreateAsync();
        var page = await harness.ListAsync(pageSize: IncidentListQueryPageSize);

        Assert.Equal(1, page.Items.Count(i => i.Id == harness.OwnAgencyCreatedBySelf));
        Assert.Equal(page.Items.Count, page.Items.Select(i => i.Id).Distinct().Count());
        Assert.Equal(page.Items.Count, page.TotalCount);
    }

    [Fact]
    public async Task Paramedic_TotalCountMatchesVisibleRows_AcrossPages()
    {
        await using var harness = await Harness.CreateAsync();
        const int pageSize = 2;

        var first = await harness.ListAsync(page: 1, pageSize: pageSize);
        Assert.Equal(Harness.VisibleCount, first.TotalCount);
        Assert.Equal(pageSize, first.Items.Count);

        var seen = new HashSet<Guid>();
        var pages = (Harness.VisibleCount + pageSize - 1) / pageSize;
        for (var page = 1; page <= pages; page++)
        {
            var result = await harness.ListAsync(page, pageSize);
            Assert.Equal(Harness.VisibleCount, result.TotalCount);
            foreach (var item in result.Items)
            {
                Assert.True(seen.Add(item.Id), $"Incident {item.Id} appeared on more than one page.");
            }
        }

        Assert.Equal(Harness.VisibleCount, seen.Count);
        Assert.Contains(harness.OwnAgencyCreatedBySelf, seen);
        Assert.Contains(harness.OwnAgencyCreatedByOther, seen);
        Assert.Contains(harness.OtherAgencyCreatedBySelf, seen);
        Assert.DoesNotContain(harness.OtherAgencyCreatedByOther, seen);
        Assert.DoesNotContain(harness.HiddenOtherAgency, seen);
    }

    private const int IncidentListQueryPageSize = 100;

    private sealed class Harness : IAsyncDisposable
    {
        public const int VisibleCount = 5;

        public required string DbPath { get; init; }
        public required SqlitePulseLinkDbContext Db { get; init; }
        public required IncidentsController Controller { get; init; }
        public required Guid OwnAgencyCreatedBySelf { get; init; }
        public required Guid OwnAgencyCreatedByOther { get; init; }
        public required Guid OtherAgencyCreatedBySelf { get; init; }
        public required Guid OtherAgencyCreatedByOther { get; init; }
        public required Guid HiddenOtherAgency { get; init; }

        public static async Task<Harness> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"pulselink-rolefilter-{Guid.NewGuid():N}.db");
            var db = new SqlitePulseLinkDbContext(
                new DbContextOptionsBuilder<SqlitePulseLinkDbContext>()
                    .UseSqlite($"Data Source={path}")
                    .Options);
            await db.Database.MigrateAsync();

            var mine = new Agency { Id = Guid.NewGuid(), Name = "Mine EMS", Region = "Test" };
            var other = new Agency { Id = Guid.NewGuid(), Name = "Other EMS", Region = "Test" };
            db.Agencies.AddRange(mine, other);

            const string paramedicId = "paramedic-under-test";
            const string otherUserId = "other-paramedic";
            var now = DateTimeOffset.UtcNow;

            var ownBySelf = Incident(mine.Id, paramedicId, now.AddMinutes(-1), "OWN-BOTH");
            var ownByOther = Incident(mine.Id, otherUserId, now.AddMinutes(-2), "OWN-AGENCY");
            var extraOwn1 = Incident(mine.Id, otherUserId, now.AddMinutes(-3), "OWN-EXTRA-1");
            var extraOwn2 = Incident(mine.Id, otherUserId, now.AddMinutes(-4), "OWN-EXTRA-2");
            var otherBySelf = Incident(other.Id, paramedicId, now.AddMinutes(-5), "OTHER-CREATED");
            var otherByOther = Incident(other.Id, otherUserId, now.AddMinutes(-6), "OTHER-HIDDEN");
            var extraHidden = Incident(other.Id, otherUserId, now.AddMinutes(-7), "OTHER-HIDDEN-2");

            db.Incidents.AddRange(
                ownBySelf, ownByOther, extraOwn1, extraOwn2, otherBySelf, otherByOther, extraHidden);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var controller = new IncidentsController(db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, paramedicId),
                            new Claim(ClaimTypes.Role, AppRoles.Paramedic),
                            new Claim("agencyId", mine.Id.ToString())
                        ], "test"))
                    }
                }
            };

            return new Harness
            {
                DbPath = path,
                Db = db,
                Controller = controller,
                OwnAgencyCreatedBySelf = ownBySelf.Id,
                OwnAgencyCreatedByOther = ownByOther.Id,
                OtherAgencyCreatedBySelf = otherBySelf.Id,
                OtherAgencyCreatedByOther = otherByOther.Id,
                HiddenOtherAgency = extraHidden.Id
            };
        }

        public async Task<PagedIncidentListDto> ListAsync(int page = 1, int pageSize = IncidentListQueryPageSize)
        {
            var result = await Controller.List(page, pageSize);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            return Assert.IsType<PagedIncidentListDto>(ok.Value);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            try { File.Delete(DbPath); } catch { /* temp */ }
        }

        private static Incident Incident(Guid agencyId, string createdBy, DateTimeOffset updated, string number)
        {
            return new Incident
            {
                Id = Guid.NewGuid(),
                IncidentNumber = number,
                Status = IncidentStatus.Draft,
                AgencyId = agencyId,
                CreatedByUserId = createdBy,
                ChiefComplaint = "Test",
                CreatedAt = updated,
                UpdatedAt = updated,
                UpdatedAtUtc = updated.UtcDateTime
            };
        }
    }
}
