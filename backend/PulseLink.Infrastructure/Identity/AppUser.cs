using Microsoft.AspNetCore.Identity;

namespace PulseLink.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public Guid? AgencyId { get; set; }
    public Guid? HospitalId { get; set; }
}
