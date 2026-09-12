using PortalApi.Domain.Entities;

namespace PortalApi.Application.Clientes;

public class ClienteService(IClienteRepository repository)
{
    private static ClienteDto ToDto(Cliente c) =>
        new(c.Id, c.Nome, c.Email, c.Empresa, c.Segmento, c.SiteUrl, c.LogoUrl, c.Status, c.CreatedAt);

    public async Task<IReadOnlyList<ClienteDto>> ListAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<ClienteDto>> ListByStatusAsync(string status, CancellationToken ct) =>
        (await repository.GetByStatusAsync(status, ct)).Select(ToDto).ToList();

    public async Task<ClienteDto> CreateAsync(UpsertClienteRequest request, CancellationToken ct)
    {
        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            Nome = request.Nome,
            Email = request.Email,
            Empresa = request.Empresa,
            Segmento = request.Segmento,
            SiteUrl = request.SiteUrl,
            LogoUrl = request.LogoUrl,
            Status = request.Status,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await repository.AddAsync(cliente, ct);
        return ToDto(cliente);
    }

    public async Task<ClienteDto?> UpdateAsync(Guid id, UpsertClienteRequest request, CancellationToken ct)
    {
        var cliente = await repository.GetByIdAsync(id, ct);
        if (cliente is null) return null;

        cliente.Nome = request.Nome;
        cliente.Email = request.Email;
        cliente.Empresa = request.Empresa;
        cliente.Segmento = request.Segmento;
        cliente.SiteUrl = request.SiteUrl;
        cliente.LogoUrl = request.LogoUrl;
        cliente.Status = request.Status;

        await repository.UpdateAsync(cliente, ct);
        return ToDto(cliente);
    }
}
