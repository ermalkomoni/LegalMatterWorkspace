using Amanet.Application.DTOs.Auth;

namespace Amanet.Application.Services;

public interface IAdminAuthenticationService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AdminIdentity?> GetAdminAsync(int id, CancellationToken cancellationToken);
}
