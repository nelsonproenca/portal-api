using System.Text.Json.Serialization;
using PortalApi.Application.Projetos;
using PortalApi.Domain.Entities;
using PortalApi.Domain.Enums;

namespace PortalApi.Api;

/// <summary>
/// Backfill único (ticket #16): copia os projetos que já existiam no Supabase pra
/// cá, preservando o Id — `etapas`, `artefatos` e `pedidos` (que só migram nos
/// próximos tickets) continuam no Supabase referenciando `projeto_id`. Uso:
/// dotnet run --project src/PortalApi.Api -- import-projetos &lt;supabase-url&gt; &lt;anon-key&gt;
/// </summary>
public static class ImportProjetosCommand
{
    public static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        var supabaseUrl = args.ElementAtOrDefault(1);
        var anonKey = args.ElementAtOrDefault(2);
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(anonKey))
        {
            Console.Error.WriteLine("Uso: dotnet run --project src/PortalApi.Api -- import-projetos <supabase-url> <anon-key>");
            return 1;
        }

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("apikey", anonKey);
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {anonKey}");

        var response = await http.GetAsync($"{supabaseUrl.TrimEnd('/')}/rest/v1/projetos?select=*");
        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<List<SupabaseProjeto>>() ?? [];
        Console.WriteLine($"{rows.Count} projetos encontrados no Supabase.");

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalApi.Infrastructure.Data.PortalDbContext>();

        var imported = 0;
        foreach (var row in rows)
        {
            if (!Guid.TryParse(row.Id, out var id) || !Guid.TryParse(row.ClienteId, out var clienteId))
            {
                Console.Error.WriteLine($"Id/cliente_id inválido, pulando: {row.Id}");
                continue;
            }

            var existing = await db.Projetos.FindAsync(id);
            if (existing is not null) continue; // backfill só cria — reexecutar é seguro (idempotente)

            db.Projetos.Add(new Projeto
            {
                Id = id,
                ClienteId = clienteId,
                Nome = row.Nome,
                Categoria = row.Categoria,
                StatusPublico = Enum.Parse<StatusPublico>(row.StatusPublico),
                Visibilidade = Enum.Parse<Visibilidade>(row.Visibilidade),
                ImagemCapaUrl = row.ImagemCapaUrl,
                LinkUrl = row.LinkUrl,
                CreatedAt = row.CreatedAt,
            });
            imported++;
        }

        await db.SaveChangesAsync();
        Console.WriteLine($"{imported} projetos importados.");
        return 0;
    }

    private record SupabaseProjeto(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("cliente_id")] string ClienteId,
        [property: JsonPropertyName("nome")] string Nome,
        [property: JsonPropertyName("categoria")] string? Categoria,
        [property: JsonPropertyName("status_publico")] string StatusPublico,
        [property: JsonPropertyName("visibilidade")] string Visibilidade,
        [property: JsonPropertyName("imagem_capa_url")] string? ImagemCapaUrl,
        [property: JsonPropertyName("link_url")] string? LinkUrl,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);
}
