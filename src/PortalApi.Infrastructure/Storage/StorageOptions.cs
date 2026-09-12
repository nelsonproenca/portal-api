namespace PortalApi.Infrastructure.Storage;

public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Diretório raiz dos artefatos dentro do container (montado como volume
    /// Docker na VPS — ver docker-compose.yml).</summary>
    public string ArtefatosRoot { get; set; } = "/app/storage/artefatos";

    /// <summary>Segredo pra assinar os tokens de download — igual em espírito ao
    /// Jwt:Secret do MediaMtx no Watchtower. Vem de configuração/env var, nunca hardcoded.</summary>
    public required string DownloadTokenSecret { get; set; }

    public int DownloadTokenTtlMinutes { get; set; } = 10;
}
