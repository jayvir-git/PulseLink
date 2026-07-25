namespace PulseLink.Core.Entities;

public class Agency
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;

    public ICollection<Incident> Incidents { get; set; } = [];
}
