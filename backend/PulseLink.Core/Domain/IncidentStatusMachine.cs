using PulseLink.Core.Enums;

namespace PulseLink.Core.Domain;

public static class IncidentStatusMachine
{
    private static readonly Dictionary<IncidentStatus, IncidentStatus[]> Allowed =
        new()
        {
            [IncidentStatus.Draft] = [IncidentStatus.EnRoute],
            [IncidentStatus.EnRoute] = [IncidentStatus.OnScene],
            [IncidentStatus.OnScene] = [IncidentStatus.Transporting],
            [IncidentStatus.Transporting] = [IncidentStatus.Arrived],
            [IncidentStatus.Arrived] = [IncidentStatus.HandedOff],
            [IncidentStatus.HandedOff] = []
        };

    public static bool CanTransition(IncidentStatus from, IncidentStatus to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);

    public static IReadOnlyList<IncidentStatus> NextStatuses(IncidentStatus current) =>
        Allowed.TryGetValue(current, out var next) ? next : [];

    public static void EnsureCanTransition(IncidentStatus from, IncidentStatus to, Guid? destinationHospitalId)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException($"Cannot transition from {from} to {to}.");
        }

        if (to is IncidentStatus.Transporting or IncidentStatus.Arrived or IncidentStatus.HandedOff
            && destinationHospitalId is null)
        {
            throw new InvalidOperationException("A destination hospital is required before transport or handoff.");
        }
    }
}
