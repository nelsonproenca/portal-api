using PortalApi.Domain.Entities;

namespace PortalApi.Application.Crm;

/// <summary>CRUD do admin para colaboradores e contatos de clientes.</summary>
public class CadastroCrmService(ICrmRepository repository)
{
    private static ColaboradorDto ToDto(Colaborador c) =>
        new(c.Id, c.Nome, c.Cargo, c.Departamento, c.Email, c.FotoUrl, c.CreatedAt);

    private static ContatoClienteDto ToDto(ContatoCliente c) =>
        new(c.Id, c.ClienteId, c.Nome, c.Telefone, c.Email, c.CreatedAt);

    // ─── Colaboradores ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ColaboradorDto>> ListarColaboradoresAsync(CancellationToken ct) =>
        (await repository.ListColaboradoresAsync(ct)).Select(ToDto).ToList();

    public async Task<CrmResult<ColaboradorDto>> CriarColaboradorAsync(UpsertColaboradorRequest request, CancellationToken ct)
    {
        var c = new Campos();
        var colaborador = new Colaborador
        {
            Id = Guid.NewGuid(),
            Nome = c.Obrigatorio(request.Nome, "Nome", 200),
            Email = c.Obrigatorio(request.Email, "E-mail", 320),
            Cargo = c.Opcional(request.Cargo, "Cargo", 200),
            Departamento = c.Opcional(request.Departamento, "Departamento", 200),
            FotoUrl = c.Opcional(request.FotoUrl, "Foto", 500),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        if (c.Erro is not null) return CrmResult<ColaboradorDto>.Invalido(c.Erro);
        if (await repository.EmailColaboradorExisteAsync(colaborador.Email, null, ct))
            return CrmResult<ColaboradorDto>.Conflito("Já existe um colaborador com esse e-mail.");

        await repository.AddAsync(colaborador, ct);
        return CrmResult<ColaboradorDto>.Sucesso(ToDto(colaborador));
    }

    public async Task<CrmResult<ColaboradorDto>> AtualizarColaboradorAsync(Guid id, UpsertColaboradorRequest request, CancellationToken ct)
    {
        var c = new Campos();
        var nome = c.Obrigatorio(request.Nome, "Nome", 200);
        var email = c.Obrigatorio(request.Email, "E-mail", 320);
        var cargo = c.Opcional(request.Cargo, "Cargo", 200);
        var departamento = c.Opcional(request.Departamento, "Departamento", 200);
        var foto = c.Opcional(request.FotoUrl, "Foto", 500);
        if (c.Erro is not null) return CrmResult<ColaboradorDto>.Invalido(c.Erro);

        var colaborador = await repository.FindAsync<Colaborador>(id, ct);
        if (colaborador is null) return CrmResult<ColaboradorDto>.NaoEncontrado();
        if (await repository.EmailColaboradorExisteAsync(email, id, ct))
            return CrmResult<ColaboradorDto>.Conflito("Já existe um colaborador com esse e-mail.");

        colaborador.Nome = nome;
        colaborador.Email = email;
        colaborador.Cargo = cargo;
        colaborador.Departamento = departamento;
        colaborador.FotoUrl = foto;
        await repository.SaveAsync(ct);
        return CrmResult<ColaboradorDto>.Sucesso(ToDto(colaborador));
    }

    public async Task<bool> ExcluirColaboradorAsync(Guid id, CancellationToken ct)
    {
        var colaborador = await repository.FindAsync<Colaborador>(id, ct);
        if (colaborador is null) return false;
        await repository.RemoveAsync(colaborador, ct);
        return true;
    }

    // ─── Contatos de clientes ─────────────────────────────────────────────────

    public async Task<IReadOnlyList<ContatoClienteDto>> ListarContatosAsync(Guid? clienteId, CancellationToken ct) =>
        (await repository.ListContatosAsync(clienteId, ct)).Select(ToDto).ToList();

    public async Task<CrmResult<ContatoClienteDto>> CriarContatoAsync(UpsertContatoClienteRequest request, CancellationToken ct)
    {
        var c = new Campos();
        var contato = new ContatoCliente
        {
            Id = Guid.NewGuid(),
            ClienteId = request.ClienteId,
            Nome = c.Obrigatorio(request.Nome, "Nome", 200),
            Telefone = c.Opcional(request.Telefone, "Telefone", 50),
            Email = c.Opcional(request.Email, "E-mail", 320),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        if (c.Erro is not null) return CrmResult<ContatoClienteDto>.Invalido(c.Erro);
        if (!await repository.ClienteExisteAsync(request.ClienteId, ct))
            return CrmResult<ContatoClienteDto>.Invalido("Cliente não encontrado.");

        await repository.AddAsync(contato, ct);
        return CrmResult<ContatoClienteDto>.Sucesso(ToDto(contato));
    }

    /// <summary>O contato não troca de cliente: <c>ClienteId</c> do corpo é ignorado na edição.</summary>
    public async Task<CrmResult<ContatoClienteDto>> AtualizarContatoAsync(Guid id, UpsertContatoClienteRequest request, CancellationToken ct)
    {
        var c = new Campos();
        var nome = c.Obrigatorio(request.Nome, "Nome", 200);
        var telefone = c.Opcional(request.Telefone, "Telefone", 50);
        var email = c.Opcional(request.Email, "E-mail", 320);
        if (c.Erro is not null) return CrmResult<ContatoClienteDto>.Invalido(c.Erro);

        var contato = await repository.FindAsync<ContatoCliente>(id, ct);
        if (contato is null) return CrmResult<ContatoClienteDto>.NaoEncontrado();

        contato.Nome = nome;
        contato.Telefone = telefone;
        contato.Email = email;
        await repository.SaveAsync(ct);
        return CrmResult<ContatoClienteDto>.Sucesso(ToDto(contato));
    }

    public async Task<bool> ExcluirContatoAsync(Guid id, CancellationToken ct)
    {
        var contato = await repository.FindAsync<ContatoCliente>(id, ct);
        if (contato is null) return false;
        await repository.RemoveAsync(contato, ct);
        return true;
    }
}
