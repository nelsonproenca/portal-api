using Microsoft.Extensions.Options;
using PortalApi.Application.Storage;

namespace PortalApi.Infrastructure.Storage;

public class LocalFileStorageService(IOptions<StorageOptions> options) : IFileStorageService
{
    private readonly StorageOptions _opts = options.Value;

    public async Task<string> SaveAsync(string relativePath, Stream content, CancellationToken ct)
    {
        var fullPath = ResolveFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, ct);

        return relativePath;
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken ct)
    {
        var fullPath = ResolveFullPath(relativePath);
        if (!File.Exists(fullPath)) return Task.FromResult<Stream?>(null);

        // FileStream aberto pro caller — ASP.NET Core (Results.Stream) cuida do
        // Dispose depois que a resposta terminar de ser escrita.
        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult<Stream?>(stream);
    }

    public void Delete(string relativePath)
    {
        var fullPath = ResolveFullPath(relativePath);
        if (File.Exists(fullPath)) File.Delete(fullPath);
    }

    private string ResolveFullPath(string relativePath)
    {
        // Barra o path traversal (ex: "../../etc/passwd") — o path relativo resolvido
        // precisa continuar dentro da raiz configurada.
        var fullPath = Path.GetFullPath(Path.Combine(_opts.ArtefatosRoot, relativePath));
        var root = Path.GetFullPath(_opts.ArtefatosRoot);
        if (!fullPath.StartsWith(root, StringComparison.Ordinal))
            throw new InvalidOperationException("Path de artefato inválido.");

        return fullPath;
    }
}
