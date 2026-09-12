namespace PortalApi.Application.Email;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string html, CancellationToken ct);
}
