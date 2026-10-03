using Microsoft.Extensions.Options;
using PortalApi.Application.Uploads;

namespace PortalApi.Infrastructure.Storage;

public class LocalUploadStorage(IOptions<StorageOptions> options) : IUploadStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.UploadsRoot);

    public async Task SaveAsync(string pasta, string arquivo, byte[] conteudo, CancellationToken ct)
    {
        var caminho = Resolver(pasta, arquivo);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        await File.WriteAllBytesAsync(caminho, conteudo, ct);
    }

    public Stream? OpenRead(string pasta, string arquivo)
    {
        var caminho = Resolver(pasta, arquivo);
        return File.Exists(caminho) ? File.OpenRead(caminho) : null;
    }

    public bool Delete(string pasta, string arquivo)
    {
        var caminho = Resolver(pasta, arquivo);
        if (!File.Exists(caminho)) return false;
        File.Delete(caminho);
        return true;
    }

    public IReadOnlyList<ArquivoInfo> List(string pasta)
    {
        var dir = ResolverPasta(pasta);
        if (!Directory.Exists(dir)) return [];
        return new DirectoryInfo(dir).EnumerateFiles()
            .Select(f => new ArquivoInfo(f.Name, f.Length, new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero)))
            .ToList();
    }

    private string ResolverPasta(string pasta) => Dentro(Path.Combine(_root, pasta));

    private string Resolver(string pasta, string arquivo) => Dentro(Path.Combine(_root, pasta, arquivo));

    /// <summary>Segunda barreira contra path traversal (a primeira é a validação de pasta e nome no serviço).</summary>
    private string Dentro(string caminho)
    {
        var completo = Path.GetFullPath(caminho);
        if (!completo.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) && completo != _root)
            throw new InvalidOperationException("Caminho de upload inválido.");
        return completo;
    }
}
