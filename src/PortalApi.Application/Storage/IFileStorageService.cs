namespace PortalApi.Application.Storage;

public interface IFileStorageService
{
    /// <summary>Salva o conteúdo no path relativo dado (ex: "{projetoId}/{uuid}-{nome}")
    /// e retorna esse mesmo path relativo, pra guardar no banco.</summary>
    Task<string> SaveAsync(string relativePath, Stream content, CancellationToken ct);

    Task<Stream?> OpenReadAsync(string relativePath, CancellationToken ct);

    void Delete(string relativePath);
}
