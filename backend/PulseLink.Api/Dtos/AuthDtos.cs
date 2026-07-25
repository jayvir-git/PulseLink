using System.ComponentModel.DataAnnotations;

namespace PulseLink.Api.Dtos;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record AuthResponse(
    string Token,
    string Email,
    string DisplayName,
    string Role,
    Guid? AgencyId,
    Guid? HospitalId);

public record MeResponse(
    string Email,
    string DisplayName,
    string Role,
    Guid? AgencyId,
    Guid? HospitalId);
