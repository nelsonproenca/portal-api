namespace PortalApi.Application.Storage;

/// <summary>Token assinado de curta duração pra download — mesmo princípio do
/// MediaMtxJwtService do Watchtower (JWT HMAC com claim de path + expiração),
/// só que pra artefatos em vez de streams de câmera.</summary>
public interface IDownloadTokenService
{
    (string Token, DateTimeOffset ExpiresAt) Generate(Guid artefatoId);

    /// <summary>Retorna o Id do artefato se o token for válido e não tiver expirado; null caso contrário.</summary>
    Guid? Validate(string token);
}
