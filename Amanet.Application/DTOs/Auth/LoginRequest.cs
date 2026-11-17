using System.ComponentModel.DataAnnotations;

namespace Amanet.Application.DTOs.Auth;

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(1024)]
    public string Password { get; init; } = string.Empty;
}
