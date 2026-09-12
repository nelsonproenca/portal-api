using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PortalApi.Application.Email;

namespace PortalApi.Infrastructure.Email;

public class ResendEmailService(HttpClient http, IOptions<ResendOptions> options) : IEmailService
{
    private readonly ResendOptions _opts = options.Value;

    public async Task SendAsync(string to, string subject, string html, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync("https://api.resend.com/emails", new
        {
            from = _opts.From,
            to = new[] { to },
            subject,
            html,
        }, ct);

        response.EnsureSuccessStatusCode();
    }
}
