using Amanet.Application.DTOs.Auth;
using Amanet.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Amanet.API.Controllers;

[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController(IAdminAuthenticationService authentication) : BaseController
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("AdminLogin")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await authentication.LoginAsync(request, cancellationToken);
        return result is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.")
            : Ok(result);
    }

    [HttpGet("me")]
    public async Task<ActionResult<AdminIdentity>> Me(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!int.TryParse(User.FindFirst("sub")?.Value, out var id))
        {
            return Unauthorized();
        }

        var admin = await authentication.GetAdminAsync(id, cancellationToken);
        return admin is null ? Forbid() : Ok(admin);
    }
}
