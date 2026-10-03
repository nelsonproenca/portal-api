using Microsoft.EntityFrameworkCore;
using PortalApi.Application.Crm;
using PortalApi.Infrastructure.Data;

namespace PortalApi.Tests;

internal static class TestDb
{
    public static PortalDbContext New(string? name = null) =>
        new(new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options);
}

/// <summary>Guarda o que o portal-api mandaria ao n8n, sem rede.</summary>
internal sealed class FakeNotificador : INotificadorN8n
{
    public List<N8nLeadPayload> Leads { get; } = [];
    public List<N8nPlaygroundPayload> Playgrounds { get; } = [];
    public List<N8nEnrichPayload> Enriches { get; } = [];

    public Task NotificarLeadAsync(N8nLeadPayload payload, CancellationToken ct) { Leads.Add(payload); return Task.CompletedTask; }
    public Task NotificarPlaygroundAsync(N8nPlaygroundPayload payload, CancellationToken ct) { Playgrounds.Add(payload); return Task.CompletedTask; }
    public Task NotificarEnrichAsync(N8nEnrichPayload payload, CancellationToken ct) { Enriches.Add(payload); return Task.CompletedTask; }
}

internal sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct)));
        return await responder(request);
    }
}
