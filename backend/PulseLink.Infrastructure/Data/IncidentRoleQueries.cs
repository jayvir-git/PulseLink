using System.Linq.Expressions;
using PulseLink.Core.Entities;
using PulseLink.Core.Enums;

namespace PulseLink.Infrastructure.Data;

public static class IncidentRoleQueries
{
    // Pass this expression to EF for lists; compile it for a loaded detail.
    // Requiring an affiliation prevents null destinations from matching null claims.
    public static Expression<Func<Incident, bool>> HospitalVisibility(Guid? hospitalId) =>
        i => hospitalId.HasValue
            && i.DestinationHospitalId == hospitalId
            && (i.Status == IncidentStatus.Transporting
                || i.Status == IncidentStatus.Arrived
                || i.Status == IncidentStatus.HandedOff);

    public static IQueryable<Incident> ForParamedic(
        IQueryable<Incident> incidents,
        Guid? agencyId,
        string createdByUserId) =>
        incidents.Where(i => i.AgencyId == agencyId)
            .Union(incidents.Where(i => i.CreatedByUserId == createdByUserId)); // distinct; Concat/UNION ALL would double-count own-agency creates
}
