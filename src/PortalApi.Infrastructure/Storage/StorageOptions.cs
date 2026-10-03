namespace PortalApi.Infrastructure.Storage;

public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Diretório raiz dos artefatos dentro do container (montado como volume
    /// Docker na VPS — ver docker-compose.yml).</summary>
    public string ArtefatosRoot { get; set; } = "/app/storage/artefatos";

    /// <summary>Raiz das imagens públicas (logos, fotos de colaboradores, convites), volume Docker próprio.</summary>
    public string UploadsRoot { get; set; } = "/app/storage/uploads";

    /// <summary>Prefixo público das imagens, como o navegador as alcança (o Caddy remove /api/portal antes de
    /// chegar aqui, mas o link devolvido ao front precisa dele).</summary>
    public string UploadsPublicBasePath { get; set; } = "/api/portal/uploads";

    /// <summary>Segredo pra assinar os tokens de download — igual em espírito ao
    /// Jwt:Secret do MediaMtx no Watchtower. Vem de configuração/env var, nunca hardcoded.</summary>
    public required string DownloadTokenSecret { get; set; }

    public int DownloadTokenTtlMinutes { get; set; } = 10;
}
