using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Amanet.Application.DTOs.Auth;
using Amanet.Application.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Amanet.Infrastructure.Identity;

public sealed class AdminAuthenticationService(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IOptions<JwtOptions> options) : IAdminAuthenticationService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return null;
        }

        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded || !await users.IsInRoleAsync(user, AdminAccess.Role))
        {
            return null;
        }

        var jwt = options.Value;
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddHours(8);
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim("role", AdminAccess.Role)
            ],
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(jwt.SigningKey)), SecurityAlgorithms.HmacSha256));

        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, ToIdentity(user));
    }

    public async Task<AdminIdentity?> GetAdminAsync(int id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(id.ToString(CultureInfo.InvariantCulture));
        return user is not null && await users.IsInRoleAsync(user, AdminAccess.Role) ? ToIdentity(user) : null;
    }

    private static AdminIdentity ToIdentity(ApplicationUser user) => new(user.Id, user.Email!, AdminAccess.Role);
}
