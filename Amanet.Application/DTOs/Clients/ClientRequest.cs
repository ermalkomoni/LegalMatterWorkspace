using System.ComponentModel.DataAnnotations;

namespace Amanet.Application.DTOs.Clients;

public sealed class ClientRequest
{
    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [StringLength(50)]
    public string? Phone { get; init; }

    [StringLength(500)]
    public string? PostalAddress { get; init; }
}
