namespace Amanet.Application.DTOs.Auth;

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, AdminIdentity Admin);
