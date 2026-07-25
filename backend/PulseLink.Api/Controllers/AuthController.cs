using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PulseLink.Api.Dtos;
using PulseLink.Api.Services;
using PulseLink.Infrastructure.Identity;

namespace PulseLink.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    JwtTokenService jwtTokenService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var roles = await userManager.GetRolesAsync(user);
        var token = jwtTokenService.CreateToken(user, roles);

        return Ok(new AuthResponse(
            token,
            user.Email!,
            user.DisplayName,
            roles.FirstOrDefault() ?? string.Empty,
            user.AgencyId,
            user.HospitalId));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        return Ok(new MeResponse(
            user.Email!,
            user.DisplayName,
            roles.FirstOrDefault() ?? string.Empty,
            user.AgencyId,
            user.HospitalId));
    }
}
