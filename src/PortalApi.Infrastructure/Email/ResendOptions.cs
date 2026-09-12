namespace PortalApi.Infrastructure.Email;

public class ResendOptions
{
    public const string SectionName = "Resend";

    public required string ApiKey { get; set; }
    public string From { get; set; } = "Portal do Cliente <noreply@nelson-proenca-info.com.br>";
}
