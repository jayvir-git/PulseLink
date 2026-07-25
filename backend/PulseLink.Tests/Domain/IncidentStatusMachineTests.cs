using PulseLink.Core.Domain;
using PulseLink.Core.Enums;

namespace PulseLink.Tests.Domain;

public class IncidentStatusMachineTests
{
    [Fact]
    public void CanTransition_DraftToEnRoute_IsAllowed()
    {
        Assert.True(IncidentStatusMachine.CanTransition(IncidentStatus.Draft, IncidentStatus.EnRoute));
    }

    [Fact]
    public void CanTransition_DraftToHandedOff_IsRejected()
    {
        Assert.False(IncidentStatusMachine.CanTransition(IncidentStatus.Draft, IncidentStatus.HandedOff));
    }

    [Fact]
    public void EnsureCanTransition_TransportWithoutHospital_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            IncidentStatusMachine.EnsureCanTransition(
                IncidentStatus.OnScene,
                IncidentStatus.Transporting,
                destinationHospitalId: null));

        Assert.Contains("destination hospital", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureCanTransition_TransportWithHospital_Succeeds()
    {
        IncidentStatusMachine.EnsureCanTransition(
            IncidentStatus.OnScene,
            IncidentStatus.Transporting,
            Guid.NewGuid());
    }

    [Fact]
    public void NextStatuses_HandedOff_IsEmpty()
    {
        Assert.Empty(IncidentStatusMachine.NextStatuses(IncidentStatus.HandedOff));
    }
}
