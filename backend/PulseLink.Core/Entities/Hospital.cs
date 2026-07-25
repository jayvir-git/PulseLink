namespace PulseLink.Core.Entities;

public class Hospital
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    public ICollection<Incident> Incidents { get; set; } = [];
}
