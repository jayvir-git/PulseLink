using PulseLink.Core.Entities;

namespace PulseLink.Infrastructure.Data;

public static class IncidentRoleQueries
{
    public static IQueryable<Incident> ForParamedic(
        IQueryable<Incident> incidents,
        Guid? agencyId,
        string createdByUserId) =>
        incidents.Where(i => i.AgencyId == agencyId)
            .Union(incidents.Where(i => i.CreatedByUserId == createdByUserId)); // distinct; Concat/UNION ALL would double-count own-agency creates
}
