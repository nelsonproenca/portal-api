using System.Text.RegularExpressions;
using PortalApi.Application.Crm;

namespace PortalApi.Application.Uploads;

public record ArquivoInfo(string Arquivo, long Tamanho, DateTimeOffset AtualizadoEm);

public record UploadDto(string Pasta, string Arquivo, string Path);

public record ArquivoAberto(Stream Conteudo, string ContentType);

/// <summary>Storage das imagens públicas (logos, fotos, convites). Recebe só pasta e nome já validados.</summary>
public interface IUploadStorage
{
    Task SaveAsync(string pasta, string arquivo, byte[] conteudo, CancellationToken ct);
    Stream? OpenRead(string pasta, string arquivo);
    bool Delete(string pasta, string arquivo);
    IReadOnlyList<ArquivoInfo> List(string pasta);
}

/// <summary>
/// Imagens públicas do site (substitui o bucket <c>uploads</c> do Supabase). O tipo vem do conteúdo (assinatura
/// do arquivo), nunca do nome ou do Content-Type enviados; o nome final é gerado aqui; pasta só da lista fixa.
/// SVG não é aceito (pode carregar script).
/// </summary>
public partial class UploadService(IUploadStorage storage)
{
    public const long TamanhoMaximo = 2 * 1024 * 1024;

    public static readonly IReadOnlyList<string> Pastas = ["clientes", "colaboradores", "convites"];

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif",
    };

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$")]
    private static partial Regex NomeValido();

    public static bool PastaValida(string? pasta) => pasta is not null && Pastas.Contains(pasta);

    /// <summary>Nome aceito: caracteres simples, sem barras, sem "..", e extensão de imagem conhecida.</summary>
    public static bool NomeSeguro(string? arquivo) =>
        arquivo is not null && NomeValido().IsMatch(arquivo) && !arquivo.Contains("..")
        && ContentTypes.ContainsKey(Path.GetExtension(arquivo));

    public static string ContentTypeDe(string arquivo) =>
        ContentTypes.GetValueOrDefault(Path.GetExtension(arquivo), "application/octet-stream");

    /// <summary>Extensão pela assinatura do arquivo, ou null se não for PNG, JPEG, GIF ou WebP.</summary>
    public static string? ExtensaoPelaAssinatura(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 8 && b[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return ".png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ".jpg";
        if (b.Length >= 6 && (b[..6].SequenceEqual("GIF87a"u8) || b[..6].SequenceEqual("GIF89a"u8))) return ".gif";
        if (b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) && b.Slice(8, 4).SequenceEqual("WEBP"u8)) return ".webp";
        return null;
    }

    public async Task<CrmResult<UploadDto>> SalvarAsync(string? pasta, Stream conteudo, CancellationToken ct)
    {
        if (!PastaValida(pasta)) return CrmResult<UploadDto>.Invalido("Pasta inválida.");

        var (bytes, erro) = await LerLimitadoAsync(conteudo, ct);
        if (erro is not null) return CrmResult<UploadDto>.Invalido(erro);

        var extensao = ExtensaoPelaAssinatura(bytes!);
        if (extensao is null) return CrmResult<UploadDto>.Invalido("Envie uma imagem PNG, JPEG, GIF ou WebP.");

        var nome = $"{Guid.NewGuid():N}{extensao}";
        await storage.SaveAsync(pasta!, nome, bytes!, ct);
        return CrmResult<UploadDto>.Sucesso(new UploadDto(pasta!, nome, $"{pasta}/{nome}"));
    }

    /// <summary>
    /// Convite com nome fixo (<c>convite-nome-da-pessoa.png</c>), regravado a cada novo QR Code: a landing procura o
    /// convite de cada colaborador por esse nome. Só a pasta "convites" aceita nome escolhido; a extensão do nome
    /// precisa ser a do conteúdo (um JPEG não entra como .png, nem um arquivo qualquer como imagem).
    /// </summary>
    public async Task<CrmResult<UploadDto>> SalvarConviteAsync(string? arquivo, Stream conteudo, CancellationToken ct)
    {
        const string pasta = "convites";
        if (!NomeSeguro(arquivo)) return CrmResult<UploadDto>.Invalido("Nome de arquivo inválido.");

        var (bytes, erro) = await LerLimitadoAsync(conteudo, ct);
        if (erro is not null) return CrmResult<UploadDto>.Invalido(erro);

        var porAssinatura = ExtensaoPelaAssinatura(bytes!);
        if (porAssinatura is null) return CrmResult<UploadDto>.Invalido("Envie uma imagem PNG, JPEG, GIF ou WebP.");
        if (NormalizarExtensao(Path.GetExtension(arquivo!)) != porAssinatura)
            return CrmResult<UploadDto>.Invalido("A extensão do nome não corresponde ao conteúdo da imagem.");

        await storage.SaveAsync(pasta, arquivo!, bytes!, ct);
        return CrmResult<UploadDto>.Sucesso(new UploadDto(pasta, arquivo!, $"{pasta}/{arquivo}"));
    }

    private static string NormalizarExtensao(string extensao) =>
        extensao.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : extensao.ToLowerInvariant();

    /// <summary>Lê no máximo o limite + 1 byte: o tamanho declarado pelo cliente não é confiável.</summary>
    private static async Task<(byte[]? Bytes, string? Erro)> LerLimitadoAsync(Stream conteudo, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var bloco = new byte[81920];
        int lidos;
        while (buffer.Length <= TamanhoMaximo
               && (lidos = await conteudo.ReadAsync(bloco.AsMemory(0, (int)Math.Min(bloco.Length, TamanhoMaximo + 1 - buffer.Length)), ct)) > 0)
            buffer.Write(bloco, 0, lidos);

        if (buffer.Length > TamanhoMaximo) return (null, $"Imagem excede o limite de {TamanhoMaximo / 1024 / 1024} MB.");
        if (buffer.Length == 0) return (null, "Arquivo vazio.");
        return (buffer.ToArray(), null);
    }

    public ArquivoAberto? Abrir(string? pasta, string? arquivo)
    {
        if (!PastaValida(pasta) || !NomeSeguro(arquivo)) return null;
        var stream = storage.OpenRead(pasta!, arquivo!);
        return stream is null ? null : new ArquivoAberto(stream, ContentTypeDe(arquivo!));
    }

    public bool Excluir(string? pasta, string? arquivo) =>
        PastaValida(pasta) && NomeSeguro(arquivo) && storage.Delete(pasta!, arquivo!);

    public IReadOnlyList<ArquivoInfo> Listar(string pasta) =>
        PastaValida(pasta) ? storage.List(pasta).Where(a => NomeSeguro(a.Arquivo)).OrderBy(a => a.Arquivo, StringComparer.Ordinal).ToList() : [];
}
