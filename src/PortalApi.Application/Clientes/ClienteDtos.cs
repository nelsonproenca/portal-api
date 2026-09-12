namespace PortalApi.Application.Clientes;

public record ClienteDto(
    Guid Id,
    string Nome,
    string Email,
    string? Empresa,
    string? Segmento,
    string? SiteUrl,
    string? LogoUrl,
    string? Status,
    DateTimeOffset CreatedAt);

public record UpsertClienteRequest(
    string Nome,
    string Email,
    string? Empresa,
    string? Segmento,
    string? SiteUrl,
    string? LogoUrl,
    string? Status);
