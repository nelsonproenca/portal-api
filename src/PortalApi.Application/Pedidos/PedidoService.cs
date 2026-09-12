using PortalApi.Application.Artefatos;
using PortalApi.Application.Email;
using PortalApi.Application.Storage;
using PortalApi.Domain.Entities;
using PortalApi.Domain.Enums;

namespace PortalApi.Application.Pedidos;

public class PedidoService(
    IPedidoRepository pedidos,
    IPedidoRespostaRepository respostas,
    IFileStorageService storage,
    PortfolioNotificationService notifications)
{
    private static PedidoDto ToDto(Pedido p) =>
        new(p.Id, p.ProjetoId, p.EtapaId, p.Tipo.ToString(), p.Titulo, p.Status.ToString(), p.CreatedAt);

    private static PedidoRespostaDto ToDto(PedidoResposta r) =>
        new(r.Id, r.PedidoId, r.Texto, r.ArquivoUrl, r.CreatedAt);

    public async Task<IReadOnlyList<PedidoDto>> ListByProjetoAsync(Guid projetoId, CancellationToken ct) =>
        (await pedidos.GetByProjetoIdAsync(projetoId, ct)).Select(ToDto).ToList();

    public async Task<Pedido?> GetAsync(Guid id, CancellationToken ct) => await pedidos.GetByIdAsync(id, ct);

    public async Task<PedidoDto> CreateAsync(CreatePedidoRequest request, CancellationToken ct)
    {
        var pedido = new Pedido
        {
            Id = Guid.NewGuid(),
            ProjetoId = request.ProjetoId,
            EtapaId = request.EtapaId,
            Tipo = Enum.Parse<PedidoTipo>(request.Tipo),
            Titulo = request.Titulo,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await pedidos.AddAsync(pedido, ct);
        await notifications.NotifyNovoPedidoAsync(pedido.ProjetoId, pedido.Titulo, ct);
        return ToDto(pedido);
    }

    public async Task<IReadOnlyList<PedidoRespostaDto>> ListRespostasAsync(Guid pedidoId, CancellationToken ct) =>
        (await respostas.GetByPedidoIdAsync(pedidoId, ct)).Select(ToDto).ToList();

    /// <summary>Cliente responde uma pergunta — marca o pedido como "respondido".
    /// Reusa a mesma validação de upload do ticket #18 (10MB, imagem/PDF/Word).</summary>
    public async Task<(PedidoRespostaDto? Dto, string? Error)> SubmitRespostaAsync(
        Guid pedidoId, Guid projetoId, string texto, Stream? file, string? fileName, string? contentType, CancellationToken ct)
    {
        var pedido = await pedidos.GetByIdAsync(pedidoId, ct);
        if (pedido is null) return (null, "Pedido não encontrado.");

        string? arquivoUrl = null;
        if (file is not null)
        {
            if (contentType is null || !ArtefatoService.IsContentTypeAllowed(contentType))
                return (null, "Tipo de arquivo não permitido. Envie uma imagem, PDF ou documento (Word).");

            var relativePath = $"{projetoId}/respostas/{Guid.NewGuid()}-{fileName}";
            arquivoUrl = await storage.SaveAsync(relativePath, file, ct);
        }

        var resposta = new PedidoResposta
        {
            Id = Guid.NewGuid(),
            PedidoId = pedidoId,
            Texto = texto,
            ArquivoUrl = arquivoUrl,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await respostas.AddAsync(resposta, ct);

        pedido.Status = PedidoStatus.respondido;
        await pedidos.SaveChangesAsync(ct);

        await notifications.NotifyPedidoRespondidoAsync(projetoId, texto, ct);
        return (ToDto(resposta), null);
    }

    public async Task<PedidoRespostaDto?> AprovarAsync(Guid pedidoId, string? comentario, CancellationToken ct)
    {
        var pedido = await pedidos.GetByIdAsync(pedidoId, ct);
        if (pedido is null) return null;

        var resposta = new PedidoResposta
        {
            Id = Guid.NewGuid(),
            PedidoId = pedidoId,
            Texto = string.IsNullOrWhiteSpace(comentario) ? "Aprovado." : comentario.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await respostas.AddAsync(resposta, ct);

        pedido.Status = PedidoStatus.aprovado;
        await pedidos.SaveChangesAsync(ct);

        await notifications.NotifyPedidoDecididoAsync(pedido.ProjetoId, $"Aprovado: {resposta.Texto}", ct);
        return ToDto(resposta);
    }

    public async Task<(PedidoRespostaDto? Dto, string? Error)> AjustarAsync(Guid pedidoId, string comentario, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(comentario))
            return (null, "Comentário é obrigatório ao pedir ajustes.");

        var pedido = await pedidos.GetByIdAsync(pedidoId, ct);
        if (pedido is null) return (null, "Pedido não encontrado.");

        var resposta = new PedidoResposta
        {
            Id = Guid.NewGuid(),
            PedidoId = pedidoId,
            Texto = comentario.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await respostas.AddAsync(resposta, ct);

        pedido.Status = PedidoStatus.ajuste_solicitado;
        await pedidos.SaveChangesAsync(ct);

        await notifications.NotifyPedidoDecididoAsync(pedido.ProjetoId, $"Ajuste solicitado: {resposta.Texto}", ct);
        return (ToDto(resposta), null);
    }
}
