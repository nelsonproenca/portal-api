using PortalApi.Application.Artefatos;
using PortalApi.Application.Clientes;
using PortalApi.Application.Projetos;

namespace PortalApi.Application.Portal;

/// <summary>
/// Mecanismo único de escopo do cliente autenticado (ticket #19) — substitui as
/// policies de RLS `scope_projetos_select_by_client_identity` /
/// `scope_etapas_artefatos_select_by_client_identity` do Postgres original.
/// Reutilizado por todo endpoint que precisa aceitar admin OU cliente.
/// </summary>
public class ClientAccessService(IClienteRepository clientes, IProjetoRepository projetos, IArtefatoRepository artefatos)
{
    public async Task<IReadOnlyList<ProjetoDto>> ListProjetosDoEmailAsync(string email, CancellationToken ct)
    {
        var cliente = await clientes.GetByEmailAsync(email, ct);
        if (cliente is null) return [];

        var todos = await projetos.GetAllAsync(ct);
        return todos.Where(p => p.ClienteId == cliente.Id)
            .Select(p => new ProjetoDto(p.Id, p.ClienteId, p.Nome, p.Categoria, p.StatusPublico.ToString(), p.Visibilidade.ToString(), p.ImagemCapaUrl, p.LinkUrl, p.CreatedAt))
            .ToList();
    }

    /// <summary>True se o e-mail dado for dono (via `clientes.email`) do projeto dado.</summary>
    public async Task<bool> OwnsProjetoAsync(string email, Guid projetoId, CancellationToken ct)
    {
        var cliente = await clientes.GetByEmailAsync(email, ct);
        if (cliente is null) return false;

        var projeto = await projetos.GetByIdAsync(projetoId, ct);
        return projeto is not null && projeto.ClienteId == cliente.Id;
    }

    /// <summary>True se o e-mail dado for dono (via o projeto do artefato) do artefato dado.</summary>
    public async Task<bool> OwnsArtefatoAsync(string email, Guid artefatoId, CancellationToken ct)
    {
        var artefato = await artefatos.GetByIdAsync(artefatoId, ct);
        return artefato is not null && await OwnsProjetoAsync(email, artefato.ProjetoId, ct);
    }
}
