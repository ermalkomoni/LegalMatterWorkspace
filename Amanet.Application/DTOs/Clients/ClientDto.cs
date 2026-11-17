namespace Amanet.Application.DTOs.Clients;

public sealed record ClientDto(int Id, string Name, string Email, string? Phone, string? PostalAddress);
