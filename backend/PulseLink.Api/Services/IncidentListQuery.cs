using PulseLink.Core.Entities;

namespace PulseLink.Api.Services;

public static class IncidentListQuery
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        return (page, pageSize);
    }

    public static IQueryable<Incident> ApplyOrderAndPage(IQueryable<Incident> query, int page, int pageSize)
    {
        (page, pageSize) = Normalize(page, pageSize);
        return query
            .OrderByDescending(i => i.UpdatedAtUtc)
            .ThenByDescending(i => i.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
    }
}
