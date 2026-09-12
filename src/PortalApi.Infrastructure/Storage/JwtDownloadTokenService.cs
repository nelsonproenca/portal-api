using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PortalApi.Application.Storage;

namespace PortalApi.Infrastructure.Storage;

public class JwtDownloadTokenService(IOptions<StorageOptions> options) : IDownloadTokenService
{
    private readonly StorageOptions _opts = options.Value;

    public (string Token, DateTimeOffset ExpiresAt) Generate(Guid artefatoId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.DownloadTokenSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTimeOffset.UtcNow.AddMinutes(_opts.DownloadTokenTtlMinutes);

        var token = new JwtSecurityToken(
            claims: [new Claim("artefato_id", artefatoId.ToString())],
            expires: expiry.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiry);
    }

    public Guid? Validate(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.DownloadTokenSecret));
        var handler = new JwtSecurityTokenHandler();

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromSeconds(30),
            }, out _);

            var claim = principal.FindFirst("artefato_id")?.Value;
            return claim is not null && Guid.TryParse(claim, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }
}
