using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PulseLink.Infrastructure.Identity;

namespace PulseLink.Api.Services;

public class JwtTokenService(IConfiguration configuration)
{
    public string CreateToken(AppUser user, IList<string> roles)
    {
        var jwt = configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var role = roles.FirstOrDefault() ?? string.Empty;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, role),
            new("agencyId", user.AgencyId?.ToString() ?? string.Empty),
            new("hospitalId", user.HospitalId?.ToString() ?? string.Empty)
        };

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
