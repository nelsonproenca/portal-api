using System.Text.Json.Serialization;
using PortalApi.Application.Clientes;
using PortalApi.Domain.Entities;

namespace PortalApi.Api;

/// <summary>
/// Backfill único (ticket #15): copia os clientes que já existiam no Supabase pra cá,
/// preservando o Id — necessário porque `contatos_clientes` (que fica no Supabase) e
/// `projetos` (que também migra, ticket #16) referenciam `clientes.id`. Roda uma vez,
/// não é um endpoint. Uso:
/// dotnet run --project src/PortalApi.Api -- import-clientes &lt;supabase-url&gt; &lt;anon-key&gt;
/// </summary>
public static class ImportClientesCommand
{
    public static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        var supabaseUrl = args.ElementAtOrDefault(1);
        var anonKey = args.ElementAtOrDefault(2);
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(anonKey))
        {
            Console.Error.WriteLine("Uso: dotnet run --project src/PortalApi.Api -- import-clientes <supabase-url> <anon-key>");
            return 1;
        }

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("apikey", anonKey);
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {anonKey}");

        var response = await http.GetAsync($"{supabaseUrl.TrimEnd('/')}/rest/v1/clientes?select=*");
        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<List<SupabaseCliente>>() ?? [];
        Console.WriteLine($"{rows.Count} clientes encontrados no Supabase.");

        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IClienteRepository>();

        var imported = 0;
        foreach (var row in rows)
        {
            if (!Guid.TryParse(row.Id, out var id))
            {
                Console.Error.WriteLine($"Id inválido, pulando: {row.Id}");
                continue;
            }

            await repository.UpsertPreservingIdAsync(new Cliente
            {
                Id = id,
                Nome = row.Nome,
                Email = row.Email,
                Empresa = row.Empresa,
                Segmento = row.Segmento,
                SiteUrl = row.SiteUrl,
                LogoUrl = row.LogoUrl,
                Status = row.Status,
                CreatedAt = row.CreatedAt,
            }, CancellationToken.None);
            imported++;
        }

        Console.WriteLine($"{imported} clientes importados/atualizados.");
        return 0;
    }

    private record SupabaseCliente(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("nome")] string Nome,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("empresa")] string? Empresa,
        [property: JsonPropertyName("segmento")] string? Segmento,
        [property: JsonPropertyName("site_url")] string? SiteUrl,
        [property: JsonPropertyName("logo_url")] string? LogoUrl,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);
}
